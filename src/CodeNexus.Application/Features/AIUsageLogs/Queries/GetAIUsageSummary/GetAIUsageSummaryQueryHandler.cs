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

        var summary = await query
            .GroupBy(x => x.UsageType)
            .Select(g => new AIUsageSummaryResponse(
                g.Key,
                g.Count(),
                g.Sum(x => (long)x.InputTokens),
                g.Sum(x => (long)x.OutputTokens),
                g.Sum(x => (long)x.TotalTokens),
                g.Sum(x => x.CostUsd)))
            .ToListAsync(cancellationToken);

        return Result<List<AIUsageSummaryResponse>>.Success(summary);
    }
}
