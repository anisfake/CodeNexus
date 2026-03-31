using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
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

        List<AIUsageSummaryResponse> summary;
        if (request.IncludeProviderModelBreakdown)
        {
            summary = await query
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
                    g.Sum(x => x.CostUsd)))
                .ToListAsync(cancellationToken);
        }
        else
        {
            summary = await query
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
                    g.Sum(x => x.CostUsd)))
                .ToListAsync(cancellationToken);
        }

        return Result<List<AIUsageSummaryResponse>>.Success(summary);
    }
}
