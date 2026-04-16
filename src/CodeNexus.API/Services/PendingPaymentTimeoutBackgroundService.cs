using CodeNexus.Domain.Enums;
using CodeNexus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.API.Services;

public class PendingPaymentTimeoutBackgroundService : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(20);
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

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeSpan.FromSeconds(60);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var policyService = scope.ServiceProvider.GetRequiredService<CodeNexus.Application.Common.Interfaces.ISystemRuntimePolicyService>();
                var policy = await policyService.GetRuntimeOperationalPolicyAsync(stoppingToken);

                delay = TimeSpan.FromSeconds(Math.Max(15, policy.PendingPaymentMonitorIntervalSeconds));
                var cutoffUtc = DateTime.UtcNow.AddMinutes(-policy.PendingPaymentTimeoutMinutes);

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
