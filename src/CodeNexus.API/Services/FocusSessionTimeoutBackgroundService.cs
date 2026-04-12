using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Domain.Enums;
using CodeNexus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.API.Services;

public class FocusSessionTimeoutBackgroundService : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FocusSessionTimeoutBackgroundService> _logger;

    public FocusSessionTimeoutBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<FocusSessionTimeoutBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(InitialDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeSpan.FromSeconds(60);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var policyService = scope.ServiceProvider.GetRequiredService<ISystemRuntimePolicyService>();
                var policy = await policyService.GetRuntimeOperationalPolicyAsync(stoppingToken);

                delay = TimeSpan.FromSeconds(Math.Max(15, policy.FocusSessionMonitorIntervalSeconds));

                var now = DateTime.UtcNow;
                var autoPauseCutoff = now.AddMinutes(-policy.FocusSessionAutoPauseAfterMinutes);
                var autoAbandonCutoff = now.AddMinutes(-policy.FocusSessionAutoAbandonAfterMinutes);

                var pausedCount = await dbContext.FocusSessions
                    .Where(fs => fs.SessionStatus == SessionStatus.Running
                                 && fs.LastActivityAt <= autoPauseCutoff)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.SessionStatus, SessionStatus.Paused)
                        .SetProperty(x => x.PausedAt, now)
                        .SetProperty(x => x.LastActivityAt, now), stoppingToken);

                var pausedToAbandon = await dbContext.FocusSessions
                    .Where(fs => fs.SessionStatus == SessionStatus.Paused
                                 && fs.LastActivityAt <= autoAbandonCutoff)
                    .ToListAsync(stoppingToken);

                var abandonedCount = 0;
                foreach (var session in pausedToAbandon)
                {
                    var endTime = now;
                    var pausedSeconds = Math.Max(0, session.TotalPausedSeconds);

                    if (session.PausedAt.HasValue)
                    {
                        var extraPaused = (int)Math.Round((endTime - session.PausedAt.Value).TotalSeconds, MidpointRounding.AwayFromZero);
                        if (extraPaused > 0)
                        {
                            pausedSeconds += extraPaused;
                        }
                    }

                    session.TotalPausedSeconds = pausedSeconds;
                    session.TotalPausedMinutes = pausedSeconds / 60;
                    session.PausedAt = null;
                    session.EndTime = endTime;
                    session.LastActivityAt = endTime;
                    var elapsedSeconds = (int)Math.Round((endTime - session.StartTime).TotalSeconds, MidpointRounding.AwayFromZero) - pausedSeconds;
                    session.ActualDurationMinutes = Math.Max(0, elapsedSeconds / 60);
                    session.SessionStatus = SessionStatus.Abandoned;
                    abandonedCount++;
                }

                if (abandonedCount > 0)
                {
                    await dbContext.SaveChangesAsync(stoppingToken);
                }

                if (pausedCount > 0 || abandonedCount > 0)
                {
                    _logger.LogInformation(
                        "Focus session timeout job: auto-paused={PausedCount}, auto-abandoned={AbandonedCount}",
                        pausedCount,
                        abandonedCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Focus session timeout job crashed.");
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
