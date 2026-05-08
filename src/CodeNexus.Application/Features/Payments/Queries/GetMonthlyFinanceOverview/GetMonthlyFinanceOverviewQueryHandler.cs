using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.AIUsageLogs;
using CodeNexus.Application.Features.Payments.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Payments.Queries.GetMonthlyFinanceOverview;

public class GetMonthlyFinanceOverviewQueryHandler
    : IRequestHandler<GetMonthlyFinanceOverviewQuery, Result<MonthlyFinanceOverviewResponse>>
{
    private readonly IApplicationDbContext _context;

    public GetMonthlyFinanceOverviewQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<MonthlyFinanceOverviewResponse>> Handle(
        GetMonthlyFinanceOverviewQuery request,
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var year = request.Year ?? nowUtc.Year;
        var month = request.Month ?? nowUtc.Month;

        if (month is < 1 or > 12 || year is < 2000 or > 3000)
        {
            return Result<MonthlyFinanceOverviewResponse>.Failure(
                "INVALID_MONTH",
                "Year or month is invalid.");
        }

        var fromUtc = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtcExclusive = fromUtc.AddMonths(1);

        var packageRevenueVnd = await _context.PaymentTransactions
            .AsNoTracking()
            .Where(x =>
                x.Status == PaymentStatus.Success &&
                x.CreatedAt >= fromUtc &&
                x.CreatedAt < toUtcExclusive)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

        var usageRows = await _context.AIUsageLogs
            .AsNoTracking()
            .Where(x => x.CreatedAt >= fromUtc && x.CreatedAt < toUtcExclusive)
            .Select(x => new
            {
                x.ConfigId,
                x.InputTokens,
                x.OutputTokens
            })
            .ToListAsync(cancellationToken);

        var configIds = usageRows
            .Where(x => x.ConfigId.HasValue)
            .Select(x => x.ConfigId!.Value)
            .Distinct()
            .ToList();

        var rateMap = new Dictionary<Guid, AIUsageCostRate>();
        if (configIds.Count > 0)
        {
            var configs = await _context.AIProviderConfigs
                .AsNoTracking()
                .Where(x => configIds.Contains(x.ConfigId))
                .Select(x => new { x.ConfigId, x.ConfigJson })
                .ToListAsync(cancellationToken);

            rateMap = AIUsageCostCalculator.BuildRateMap(configs.Select(x => (x.ConfigId, (string?)x.ConfigJson)));
        }

        var aiCostUsd = usageRows.Sum(row =>
        {
            if (!row.ConfigId.HasValue || !rateMap.TryGetValue(row.ConfigId.Value, out var rate))
            {
                return 0m;
            }

            return AIUsageCostCalculator.CalculateRawCostUsd(row.InputTokens, row.OutputTokens, rate);
        });

        return Result<MonthlyFinanceOverviewResponse>.Success(new MonthlyFinanceOverviewResponse(
            Round2(packageRevenueVnd),
            Round8(aiCostUsd)));
    }

    private static decimal Round2(decimal value)
        => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal Round8(decimal value)
        => decimal.Round(value, 8, MidpointRounding.AwayFromZero);
}
