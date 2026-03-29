using CodeNexus.Application.Features.Notifications.Commands.CreateOverdueNotifications;
using MediatR;

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
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
