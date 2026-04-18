using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.AIUsageLogs;
using CodeNexus.Application.Features.AIUsageLogs.DTOs;
using CodeNexus.Application.Features.Payments;
using CodeNexus.Application.Features.SystemRuntimePolicies;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.AIUsageLogs.Queries.GetAIUsageSummary;

public class GetAIUsageSummaryQueryHandler : IRequestHandler<GetAIUsageSummaryQuery, Result<List<AIUsageSummaryResponse>>>
{
    private readonly IApplicationDbContext _context;

    public GetAIUsageSummaryQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<AIUsageSummaryResponse>>> Handle(GetAIUsageSummaryQuery request, CancellationToken cancellationToken)
    {
        var query = _context.AIUsageLogs.AsNoTracking().AsQueryable();

        if (request.FromDate.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(x => x.CreatedAt <= request.ToDate.Value);
        }

        var rows = await query
            .Select(x => new UsageSummaryRow(
                x.UserId,
                x.ConfigId,
                x.AccessTierUsed,
                x.UsageType,
                x.ProviderName,
                x.Model,
                x.InputTokens,
                x.OutputTokens,
                x.TotalTokens,
                x.ChargedTokens))
            .ToListAsync(cancellationToken);

        var rateMap = await LoadRateMapAsync(rows.Select(x => x.ConfigId), cancellationToken);
        var roleMap = await LoadRoleMapAsync(rows.Select(x => x.UserId), cancellationToken);
        var usdPerToken = await ResolveUsdPerTokenAsync(cancellationToken);

        if (request.IncludeProviderModelBreakdown)
        {
            var summary = rows
                .GroupBy(x => new { x.AccessTierUsed, x.UsageType, x.ProviderName, x.Model })
                .Select(g => BuildSummaryResponse(
                    g.Key.AccessTierUsed,
                    g.Key.UsageType,
                    g.Key.ProviderName,
                    g.Key.Model,
                    g.ToList(),
                    rateMap,
                    roleMap,
                    usdPerToken))
                .ToList();

            return Result<List<AIUsageSummaryResponse>>.Success(summary);
        }

        var aggregated = rows
            .GroupBy(x => new { x.AccessTierUsed, x.UsageType })
            .Select(g => BuildSummaryResponse(
                g.Key.AccessTierUsed,
                g.Key.UsageType,
                "All",
                "All",
                g.ToList(),
                rateMap,
                roleMap,
                usdPerToken))
            .ToList();

        return Result<List<AIUsageSummaryResponse>>.Success(aggregated);
    }

    private async Task<Dictionary<Guid, AIUsageCostRate>> LoadRateMapAsync(
        IEnumerable<Guid?> configIds,
        CancellationToken cancellationToken)
    {
        var ids = configIds
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<Guid, AIUsageCostRate>();
        }

        var configs = await _context.AIProviderConfigs
            .AsNoTracking()
            .Where(x => ids.Contains(x.ConfigId))
            .Select(x => new { x.ConfigId, x.ConfigJson })
            .ToListAsync(cancellationToken);

        return AIUsageCostCalculator.BuildRateMap(configs.Select(x => (x.ConfigId, (string?)x.ConfigJson)));
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

    private async Task<decimal> ResolveUsdPerTokenAsync(CancellationToken cancellationToken)
    {
        const decimal fallbackUsdPerToken = 0m;

        try
        {
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
            return ReadPositiveDecimal(config, "usdPerToken", fallbackUsdPerToken);
        }
        catch
        {
            return fallbackUsdPerToken;
        }
    }

    private static decimal ReadPositiveDecimal(
        IReadOnlyDictionary<string, object> config,
        string key,
        decimal fallback)
    {
        if (!config.TryGetValue(key, out var rawValue))
        {
            return fallback;
        }

        var parsed = rawValue switch
        {
            decimal d when d > 0m => d,
            double d when d > 0d => (decimal)d,
            float f when f > 0f => (decimal)f,
            int i when i > 0 => i,
            long l when l > 0 => l,
            string s when decimal.TryParse(s, out var value) && value > 0m => value,
            _ => 0m
        };

        return parsed > 0m ? parsed : fallback;
    }

    private static decimal ResolveCostUsd(UsageSummaryRow row, IReadOnlyDictionary<Guid, AIUsageCostRate> rateMap)
    {
        if (!row.ConfigId.HasValue || !rateMap.TryGetValue(row.ConfigId.Value, out var rate))
        {
            return 0m;
        }

        return AIUsageCostCalculator.CalculateRawCostUsd(row.InputTokens, row.OutputTokens, rate);
    }

    private static decimal ResolveRawChargedTokens(UsageSummaryRow row, IReadOnlyDictionary<Guid, AIUsageCostRate> rateMap)
    {
        if (!row.ConfigId.HasValue || !rateMap.TryGetValue(row.ConfigId.Value, out var rate))
        {
            return 0m;
        }

        return AIUsageCostCalculator.CalculateRawCostUsd(row.InputTokens, row.OutputTokens, rate);
    }

    private static bool IsBillableStudentPaidCall(UsageSummaryRow row, IReadOnlyDictionary<Guid, string> roleMap)
    {
        if (row.AccessTierUsed != Domain.Enums.AIAccessTier.Paid || !row.UserId.HasValue)
        {
            return false;
        }

        if (!roleMap.TryGetValue(row.UserId.Value, out var roleName))
        {
            return false;
        }

        return !string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase)
               && !string.Equals(roleName, "Mentor", StringComparison.OrdinalIgnoreCase);
    }

    private static decimal ResolveRevenueUsd(UsageSummaryRow row, IReadOnlyDictionary<Guid, string> roleMap, decimal usdPerToken)
    {
        if (!IsBillableStudentPaidCall(row, roleMap) || row.ChargedTokens <= 0m || usdPerToken <= 0m)
        {
            return 0m;
        }

        return decimal.Round(row.ChargedTokens * usdPerToken, 8, MidpointRounding.AwayFromZero);
    }

    private static decimal ResolveRawRevenueUsd(
        UsageSummaryRow row,
        IReadOnlyDictionary<Guid, string> roleMap,
        IReadOnlyDictionary<Guid, AIUsageCostRate> rateMap)
    {
        if (!IsBillableStudentPaidCall(row, roleMap))
        {
            return 0m;
        }

        var rawChargedTokens = ResolveRawChargedTokens(row, rateMap);
        if (rawChargedTokens <= 0m)
        {
            return 0m;
        }

        return rawChargedTokens;
    }

    private static AIUsageSummaryResponse BuildSummaryResponse(
        Domain.Enums.AIAccessTier accessTierUsed,
        Domain.Enums.AIUsageType usageType,
        string providerName,
        string model,
        List<UsageSummaryRow> rows,
        IReadOnlyDictionary<Guid, AIUsageCostRate> rateMap,
        IReadOnlyDictionary<Guid, string> roleMap,
        decimal usdPerToken)
    {
        var totalRequests = rows.Count;
        var totalInputTokens = rows.Sum(x => (long)x.InputTokens);
        var totalOutputTokens = rows.Sum(x => (long)x.OutputTokens);
        var totalTokens = rows.Sum(x => (long)x.TotalTokens);
        var totalChargedTokens = rows.Sum(x => x.ChargedTokens);
        var totalRawChargedTokens = rows.Sum(x => ResolveRawChargedTokens(x, rateMap));
        var totalCostUsd = rows.Sum(x => ResolveCostUsd(x, rateMap));
        var totalRevenueUsd = rows.Sum(x => ResolveRevenueUsd(x, roleMap, usdPerToken));
        var totalRawRevenueUsd = rows.Sum(x => ResolveRawRevenueUsd(x, roleMap, rateMap));

        totalRawChargedTokens = decimal.Round(totalRawChargedTokens, 8, MidpointRounding.AwayFromZero);
        totalCostUsd = decimal.Round(totalCostUsd, 8, MidpointRounding.AwayFromZero);
        totalRevenueUsd = decimal.Round(totalRevenueUsd, 8, MidpointRounding.AwayFromZero);
        totalRawRevenueUsd = decimal.Round(totalRawRevenueUsd, 8, MidpointRounding.AwayFromZero);

        var totalProfitUsd = decimal.Round(totalRevenueUsd - totalCostUsd, 8, MidpointRounding.AwayFromZero);
        var totalRawProfitUsd = decimal.Round(totalRawRevenueUsd - totalCostUsd, 8, MidpointRounding.AwayFromZero);
        var profitMarginPercent = totalRevenueUsd > 0m
            ? decimal.Round((totalProfitUsd / totalRevenueUsd) * 100m, 2, MidpointRounding.AwayFromZero)
            : (decimal?)null;

        return new AIUsageSummaryResponse(
            accessTierUsed,
            usageType,
            providerName,
            model,
            totalRequests,
            totalInputTokens,
            totalOutputTokens,
            totalTokens,
            totalChargedTokens,
            totalRawChargedTokens,
            totalCostUsd,
            totalRevenueUsd,
            totalRawRevenueUsd,
            totalProfitUsd,
            totalRawProfitUsd,
            profitMarginPercent);
    }

    private sealed record UsageSummaryRow(
        Guid? UserId,
        Guid? ConfigId,
        Domain.Enums.AIAccessTier AccessTierUsed,
        Domain.Enums.AIUsageType UsageType,
        string ProviderName,
        string Model,
        int InputTokens,
        int OutputTokens,
        int TotalTokens,
        decimal ChargedTokens);
}

