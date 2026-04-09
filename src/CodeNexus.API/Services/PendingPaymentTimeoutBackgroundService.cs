using CodeNexus.Domain.Enums;
using CodeNexus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.API.Services;

public class PendingPaymentTimeoutBackgroundService : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan PendingTimeout = TimeSpan.FromMinutes(15);
    private const string ExpiredResponseCode = "EXPIRED_TIMEOUT";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PendingPaymentTimeoutBackgroundService> _logger;

    public PendingPaymentTimeoutBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<PendingPaymentTimeoutBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(InitialDelay, stoppingToken);

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var cutoffUtc = DateTime.UtcNow - PendingTimeout;

                var affectedRows = await dbContext.PaymentTransactions
                    .Where(x => x.Status == PaymentStatus.Pending && x.CreatedAt <= cutoffUtc)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.Status, PaymentStatus.Canceled)
                        .SetProperty(x => x.ResponseCode, ExpiredResponseCode)
                        .SetProperty(x => x.UpdatedAt, DateTime.UtcNow), stoppingToken);

                if (affectedRows > 0)
                {
                    _logger.LogInformation(
                        "Auto-canceled {Count} expired pending payment(s). CutoffUtc={CutoffUtc}",
                        affectedRows,
                        cutoffUtc);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pending payment timeout job crashed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
