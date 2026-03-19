using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
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
            _ => request.SortDescending
                ? query.OrderByDescending(x => x.CreatedAt)
                : query.OrderBy(x => x.CreatedAt)
        };

        var items = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new AIUsageLogResponse(
                x.UsageLogId,
                x.UsageType,
                x.ProviderName,
                x.Model,
                x.InputTokens,
                x.OutputTokens,
                x.TotalTokens,
                x.CostUsd,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        var result = new PaginationDto<AIUsageLogResponse>
        {
            Items = items,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalCount = totalCount
        };

        return Result<PaginationDto<AIUsageLogResponse>>.Success(result);
    }
}
