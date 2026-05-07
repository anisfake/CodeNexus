using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.AIUsageLogs;
using CodeNexus.Application.Features.Payments.DTOs;
using CodeNexus.Application.Features.SystemRuntimePolicies;
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
                x.UserId,
                x.ConfigId,
                x.AccessTierUsed,
                x.InputTokens,
                x.OutputTokens,
                x.ChargedTokens
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

        var usdPerToken = await ResolveUsdPerTokenAsync(cancellationToken);
        var roleMap = await LoadRoleMapAsync(usageRows.Select(x => x.UserId), cancellationToken);

        var aiRevenueUsd = usageRows.Sum(row =>
        {
            if (row.AccessTierUsed != Domain.Enums.AIAccessTier.Paid)
            {
                return 0m;
            }

            if (!row.UserId.HasValue || !roleMap.TryGetValue(row.UserId.Value, out var roleName))
            {
                return 0m;
            }

            if (!string.Equals(roleName, "Student", StringComparison.OrdinalIgnoreCase))
            {
                return 0m;
            }

            if (row.ChargedTokens <= 0m || usdPerToken <= 0m)
            {
                return 0m;
            }

            return row.ChargedTokens * usdPerToken;
        });

        var aiProfitUsd = aiRevenueUsd - aiCostUsd;

        return Result<MonthlyFinanceOverviewResponse>.Success(new MonthlyFinanceOverviewResponse(
            Round2(packageRevenueVnd),
            Round8(aiProfitUsd)));
    }

    private async Task<decimal> ResolveUsdPerTokenAsync(CancellationToken cancellationToken)
    {
        const decimal fallbackUsdPerToken = 0m;

        var policy = await _context.SystemRuntimePolicies
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.PolicyKey == TokenPricingConstants.TokenPricingPolicyKey && x.IsActive,
                cancellationToken);

        if (policy == null)
        {
            return fallbackUsdPerToken;
        }

        var config = SystemRuntimePolicyJsonHelper.ParseConfigJson(policy.ConfigJson);
        return ReadPositiveDecimal(config, "usdPerToken");
    }

    private async Task<Dictionary<Guid, string>> LoadRoleMapAsync(
        IEnumerable<Guid?> userIds,
        CancellationToken cancellationToken)
    {
        var ids = userIds
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var users = await _context.Users
            .AsNoTracking()
            .Where(x => ids.Contains(x.UserId))
            .Select(x => new
            {
                x.UserId,
                RoleName = x.Role != null ? x.Role.RoleName : string.Empty
            })
            .ToListAsync(cancellationToken);

        return users.ToDictionary(x => x.UserId, x => x.RoleName ?? string.Empty);
    }

    private static decimal ReadPositiveDecimal(IReadOnlyDictionary<string, object> config, string key)
    {
        if (!config.TryGetValue(key, out var rawValue))
        {
            return 0m;
        }

        return rawValue switch
        {
            decimal d when d > 0m => d,
            double d when d > 0d => (decimal)d,
            float f when f > 0f => (decimal)f,
            int i when i > 0 => i,
            long l when l > 0 => l,
            string s when decimal.TryParse(s, out var value) && value > 0m => value,
            _ => 0m
        };
    }

    private static decimal Round2(decimal value)
        => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal Round8(decimal value)
        => decimal.Round(value, 8, MidpointRounding.AwayFromZero);
}
