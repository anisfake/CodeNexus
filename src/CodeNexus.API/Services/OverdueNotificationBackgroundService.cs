using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Notifications.Commands.CreateOverdueNotifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.API.Services;

public class OverdueNotificationBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OverdueNotificationBackgroundService> _logger;

    public OverdueNotificationBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<OverdueNotificationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

                var eligibleUserIds = await GetEligibleUserIdsAsync(context, stoppingToken);
                if (eligibleUserIds.Count == 0)
                {
                    continue;
                }

                var result = await sender.Send(new CreateOverdueNotificationsCommand(eligibleUserIds), stoppingToken);
                if (result.IsFailure)
                {
                    _logger.LogWarning("Overdue notification job failed: {ErrorCode} - {ErrorMessage}", result.ErrorCode, result.ErrorMessage);
                }
                else if (result.Value is { CreatedCount: > 0 })
                {
                    _logger.LogInformation("Overdue notification job created {Count} notifications.", result.Value.CreatedCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Overdue notification job crashed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private static async Task<List<Guid>> GetEligibleUserIdsAsync(IApplicationDbContext context, CancellationToken cancellationToken)
    {
        var timezone = ResolveVietnamTimeZone();
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timezone);
        var previousTickLocal = nowLocal.Subtract(Interval);

        var profiles = await context.UserProfiles
            .AsNoTracking()
            .Where(x => x.DailyReminderTime.HasValue)
            .Select(x => new { x.UserId, ReminderTime = x.DailyReminderTime!.Value })
            .ToListAsync(cancellationToken);

        return profiles
            .Where(x => IsInWindow(x.ReminderTime, previousTickLocal.TimeOfDay, nowLocal.TimeOfDay))
            .Select(x => x.UserId)
            .Distinct()
            .ToList();
    }

    private static bool IsInWindow(TimeSpan value, TimeSpan previous, TimeSpan current)
    {
        if (previous <= current)
        {
            return value > previous && value <= current;
        }

        return value > previous || value <= current;
    }

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
    }
}
