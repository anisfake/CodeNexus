using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.AIUsageLogs;
using CodeNexus.Application.Features.AIUsageLogs.DTOs;
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

        if (request.IncludeProviderModelBreakdown)
        {
            var summary = rows
                .GroupBy(x => new { x.AccessTierUsed, x.UsageType, x.ProviderName, x.Model })
                .Select(g => new AIUsageSummaryResponse(
                    g.Key.AccessTierUsed,
                    g.Key.UsageType,
                    g.Key.ProviderName,
                    g.Key.Model,
                    g.Count(),
                    g.Sum(x => (long)x.InputTokens),
                    g.Sum(x => (long)x.OutputTokens),
                    g.Sum(x => (long)x.TotalTokens),
                    g.Sum(x => x.ChargedTokens),
                    g.Sum(x => ResolveCostUsd(x, rateMap))))
                .ToList();

            return Result<List<AIUsageSummaryResponse>>.Success(summary);
        }

        var aggregated = rows
            .GroupBy(x => new { x.AccessTierUsed, x.UsageType })
            .Select(g => new AIUsageSummaryResponse(
                g.Key.AccessTierUsed,
                g.Key.UsageType,
                "All",
                "All",
                g.Count(),
                g.Sum(x => (long)x.InputTokens),
                g.Sum(x => (long)x.OutputTokens),
                g.Sum(x => (long)x.TotalTokens),
                g.Sum(x => x.ChargedTokens),
                g.Sum(x => ResolveCostUsd(x, rateMap))))
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

    private static decimal ResolveCostUsd(UsageSummaryRow row, IReadOnlyDictionary<Guid, AIUsageCostRate> rateMap)
    {
        if (!row.ConfigId.HasValue || !rateMap.TryGetValue(row.ConfigId.Value, out var rate))
        {
            return 0m;
        }

        return AIUsageCostCalculator.CalculateCostUsd(row.InputTokens, row.OutputTokens, rate);
    }

    private sealed record UsageSummaryRow(
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

