using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.AIUsageLogs.DTOs;
using CodeNexus.Application.Features.Payments;
using CodeNexus.Application.Features.SystemRuntimePolicies;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.AIUsageLogs.Queries.GetAIProfitOverview;

public class GetAIProfitOverviewQueryHandler
    : IRequestHandler<GetAIProfitOverviewQuery, Result<AIProfitOverviewResponse>>
{
    private readonly IApplicationDbContext _context;

    public GetAIProfitOverviewQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<AIProfitOverviewResponse>> Handle(
        GetAIProfitOverviewQuery request,
        CancellationToken cancellationToken)
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
            .Select(x => new UsageRow(
                x.UserId,
                x.ConfigId,
                x.AccessTierUsed,
                x.InputTokens,
                x.OutputTokens,
                x.ChargedTokens))
            .ToListAsync(cancellationToken);

        var rateMap = await LoadRateMapAsync(rows.Select(x => x.ConfigId), cancellationToken);
        var roleMap = await LoadRoleMapAsync(rows.Select(x => x.UserId), cancellationToken);
        var usdPerToken = await ResolveUsdPerTokenAsync(cancellationToken);

        decimal systemCostFree = 0m;
        decimal systemCostPaid = 0m;
        decimal studentUsageFree = 0m;
        decimal studentUsagePaid = 0m;
        decimal studentUsageCost = 0m;
        decimal studentUsageRaw = 0m;
        decimal studentBilledRevenue = 0m;
        decimal totalRevenueFree = 0m;
        decimal totalRevenuePaid = 0m;
        decimal studentRevenueRaw = 0m;

        foreach (var row in rows)
        {
            var costUsd = ResolveCostUsd(row, rateMap);

            if (costUsd > 0m && row.AccessTierUsed == Domain.Enums.AIAccessTier.Free)
            {
                systemCostFree += costUsd;
            }
            else if (costUsd > 0m && row.AccessTierUsed == Domain.Enums.AIAccessTier.Paid)
            {
                systemCostPaid += costUsd;
            }

            if (IsStudentCall(row, roleMap))
            {
                if (costUsd > 0m && row.AccessTierUsed == Domain.Enums.AIAccessTier.Free)
                {
                    studentUsageFree += costUsd;
                }
                else if (costUsd > 0m && row.AccessTierUsed == Domain.Enums.AIAccessTier.Paid)
                {
                    studentUsagePaid += costUsd;
                    studentUsageCost += costUsd;
                }
            }

            if (IsStudentPaidCall(row, roleMap))
            {
                if (usdPerToken > 0m)
                {
                    var rawChargedTokens = ResolveRawChargedTokens(row, rateMap);
                    if (rawChargedTokens > 0m)
                    {
                        // Student usage amount (no rounding at per-request level).
                        studentUsageRaw += rawChargedTokens * usdPerToken;
                    }

                    if (row.ChargedTokens > 0m)
                    {
                        // Student billed amount (already rounded at per-request level).
                        studentBilledRevenue += row.ChargedTokens * usdPerToken;
                    }
                }
            }
        }

        systemCostFree = Round8(systemCostFree);
        systemCostPaid = Round8(systemCostPaid);
        var systemCostTotal = Round8(systemCostFree + systemCostPaid);
        studentUsageFree = Round8(studentUsageFree);
        studentUsagePaid = Round8(studentUsagePaid);
        studentUsageCost = Round8(studentUsageCost);
        studentUsageRaw = Round8(studentUsageRaw);
        studentBilledRevenue = Round8(studentBilledRevenue);
        totalRevenueFree = 0m;
        totalRevenuePaid = studentBilledRevenue;
        totalRevenuePaid = Round8(totalRevenuePaid);
        var totalRevenue = Round8(totalRevenueFree + totalRevenuePaid);
        var totalProfit = Round8(totalRevenue - systemCostTotal);
        studentRevenueRaw = studentUsageRaw;
        var rawProfit = Round8(studentRevenueRaw - systemCostTotal);

        return Result<AIProfitOverviewResponse>.Success(new AIProfitOverviewResponse(
            request.FromDate,
            request.ToDate,
            systemCostFree,
            systemCostPaid,
            systemCostTotal,
            studentUsageFree,
            studentUsagePaid,
            studentUsageCost,
            studentUsageRaw,
            studentBilledRevenue,
            totalRevenueFree,
            totalRevenuePaid,
            totalRevenue,
            totalProfit,
            studentRevenueRaw,
            rawProfit));
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
            if (!config.TryGetValue("usdPerToken", out var rawValue))
            {
                return fallbackUsdPerToken;
            }

            return rawValue switch
            {
                decimal d when d > 0m => d,
                double d when d > 0d => (decimal)d,
                float f when f > 0f => (decimal)f,
                int i when i > 0 => i,
                long l when l > 0 => l,
                string s when decimal.TryParse(s, out var value) && value > 0m => value,
                _ => fallbackUsdPerToken
            };
        }
        catch
        {
            return fallbackUsdPerToken;
        }
    }

    private static decimal ResolveCostUsd(UsageRow row, IReadOnlyDictionary<Guid, AIUsageCostRate> rateMap)
    {
        if (!row.ConfigId.HasValue || !rateMap.TryGetValue(row.ConfigId.Value, out var rate))
        {
            return 0m;
        }

        return AIUsageCostCalculator.CalculateRawCostUsd(row.InputTokens, row.OutputTokens, rate);
    }

    private static decimal ResolveRawChargedTokens(UsageRow row, IReadOnlyDictionary<Guid, AIUsageCostRate> rateMap)
    {
        if (!row.ConfigId.HasValue || !rateMap.TryGetValue(row.ConfigId.Value, out var rate))
        {
            return 0m;
        }

        return AIUsageCostCalculator.CalculateRawCostUsd(row.InputTokens, row.OutputTokens, rate);
    }

    private static bool IsStudentPaidCall(UsageRow row, IReadOnlyDictionary<Guid, string> roleMap)
    {
        return row.AccessTierUsed == Domain.Enums.AIAccessTier.Paid
               && IsStudentCall(row, roleMap);
    }

    private static bool IsStudentCall(UsageRow row, IReadOnlyDictionary<Guid, string> roleMap)
    {
        if (!row.UserId.HasValue)
        {
            return false;
        }

        if (!roleMap.TryGetValue(row.UserId.Value, out var roleName))
        {
            return false;
        }

        return string.Equals(roleName, "Student", StringComparison.OrdinalIgnoreCase);
    }

    private static decimal Round8(decimal value)
        => decimal.Round(value, 8, MidpointRounding.AwayFromZero);

    private sealed record UsageRow(
        Guid? UserId,
        Guid? ConfigId,
        Domain.Enums.AIAccessTier AccessTierUsed,
        int InputTokens,
        int OutputTokens,
        decimal ChargedTokens);
}
