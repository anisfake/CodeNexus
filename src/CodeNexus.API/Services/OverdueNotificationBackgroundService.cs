using CodeNexus.Application.Features.Notifications.Commands.CreateOverdueNotifications;
using MediatR;

namespace CodeNexus.API.Services;

public class OverdueNotificationBackgroundService : BackgroundService
{
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

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeSpan.FromMinutes(15);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                var policyService = scope.ServiceProvider.GetRequiredService<CodeNexus.Application.Common.Interfaces.ISystemRuntimePolicyService>();
                var policy = await policyService.GetRuntimeOperationalPolicyAsync(stoppingToken);

                delay = TimeSpan.FromMinutes(Math.Max(5, policy.OverdueNotificationIntervalMinutes));

                var result = await sender.Send(new CreateOverdueNotificationsCommand(), stoppingToken);
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
