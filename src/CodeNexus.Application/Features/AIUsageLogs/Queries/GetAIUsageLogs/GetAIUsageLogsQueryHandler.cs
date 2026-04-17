using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.AIUsageLogs;
using CodeNexus.Application.Features.AIUsageLogs.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.AIUsageLogs.Queries.GetAIUsageLogs;

public class GetAIUsageLogsQueryHandler : IRequestHandler<GetAIUsageLogsQuery, Result<PaginationDto<AIUsageLogResponse>>>
{
    private readonly IApplicationDbContext _context;

    public GetAIUsageLogsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PaginationDto<AIUsageLogResponse>>> Handle(GetAIUsageLogsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.AIUsageLogs.AsNoTracking().AsQueryable();

        if (request.UsageType.HasValue)
        {
            query = query.Where(x => x.UsageType == request.UsageType.Value);
        }

        if (request.AccessTierUsed.HasValue)
        {
            query = query.Where(x => x.AccessTierUsed == request.AccessTierUsed.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.ProviderName))
        {
            var provider = request.ProviderName.Trim().ToLower();
            query = query.Where(x => x.ProviderName.ToLower().Contains(provider));
        }

        if (request.FromDate.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(x => x.CreatedAt <= request.ToDate.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var pageIndex = Math.Max(request.PageNumber - 1, 0);
        var pageSize = Math.Max(request.PageSize, 1);

        List<AIUsageLogResponse> items;
        if (request.SortBy == AIUsageLogSortBy.CostUsd)
        {
            var rows = await query
                .Select(x => new UsageLogProjection(
                    x.UsageLogId,
                    x.UserId,
                    x.ConfigId,
                    x.UsageType,
                    x.AccessTierUsed,
                    x.ProviderName,
                    x.Model,
                    x.InputTokens,
                    x.OutputTokens,
                    x.TotalTokens,
                    x.ChargedTokens,
                    x.CreatedAt))
                .ToListAsync(cancellationToken);

            var rateMap = await LoadRateMapAsync(rows.Select(x => x.ConfigId), cancellationToken);

            var withCost = rows
                .Select(x => new
                {
                    Row = x,
                    CostUsd = ResolveCostUsd(x, rateMap)
                });

            var ordered = request.SortDescending
                ? withCost.OrderByDescending(x => x.CostUsd).ThenByDescending(x => x.Row.CreatedAt)
                : withCost.OrderBy(x => x.CostUsd).ThenBy(x => x.Row.CreatedAt);

            items = ordered
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .Select(x => ToResponse(x.Row, x.CostUsd))
                .ToList();
        }
        else
        {
            query = request.SortBy switch
            {
                AIUsageLogSortBy.TotalTokens => request.SortDescending
                    ? query.OrderByDescending(x => x.TotalTokens)
                    : query.OrderBy(x => x.TotalTokens),
                AIUsageLogSortBy.InputTokens => request.SortDescending
                    ? query.OrderByDescending(x => x.InputTokens)
                    : query.OrderBy(x => x.InputTokens),
                AIUsageLogSortBy.OutputTokens => request.SortDescending
                    ? query.OrderByDescending(x => x.OutputTokens)
                    : query.OrderBy(x => x.OutputTokens),
                AIUsageLogSortBy.ChargedTokens => request.SortDescending
                    ? query.OrderByDescending(x => x.ChargedTokens)
                    : query.OrderBy(x => x.ChargedTokens),
                _ => request.SortDescending
                    ? query.OrderByDescending(x => x.CreatedAt)
                    : query.OrderBy(x => x.CreatedAt)
            };

            var rows = await query
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .Select(x => new UsageLogProjection(
                    x.UsageLogId,
                    x.UserId,
                    x.ConfigId,
                    x.UsageType,
                    x.AccessTierUsed,
                    x.ProviderName,
                    x.Model,
                    x.InputTokens,
                    x.OutputTokens,
                    x.TotalTokens,
                    x.ChargedTokens,
                    x.CreatedAt))
                .ToListAsync(cancellationToken);

            var rateMap = await LoadRateMapAsync(rows.Select(x => x.ConfigId), cancellationToken);
            items = rows
                .Select(x => ToResponse(x, ResolveCostUsd(x, rateMap)))
                .ToList();
        }

        var result = new PaginationDto<AIUsageLogResponse>
        {
            Items = items,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalCount = totalCount
        };

        return Result<PaginationDto<AIUsageLogResponse>>.Success(result);
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

    private static decimal ResolveCostUsd(UsageLogProjection row, IReadOnlyDictionary<Guid, AIUsageCostRate> rateMap)
    {
        if (!row.ConfigId.HasValue || !rateMap.TryGetValue(row.ConfigId.Value, out var rate))
        {
            return 0m;
        }

        return AIUsageCostCalculator.CalculateCostUsd(row.InputTokens, row.OutputTokens, rate);
    }

    private static AIUsageLogResponse ToResponse(UsageLogProjection row, decimal costUsd)
        => new(
            row.UsageLogId,
            row.UserId,
            row.ConfigId,
            row.UsageType,
            row.AccessTierUsed,
            row.ProviderName,
            row.Model,
            row.InputTokens,
            row.OutputTokens,
            row.TotalTokens,
            row.ChargedTokens,
            costUsd,
            row.CreatedAt);

    private sealed record UsageLogProjection(
        Guid UsageLogId,
        Guid? UserId,
        Guid? ConfigId,
        Domain.Enums.AIUsageType UsageType,
        Domain.Enums.AIAccessTier AccessTierUsed,
        string ProviderName,
        string Model,
        int InputTokens,
        int OutputTokens,
        int TotalTokens,
        decimal ChargedTokens,
        DateTime CreatedAt);
}

