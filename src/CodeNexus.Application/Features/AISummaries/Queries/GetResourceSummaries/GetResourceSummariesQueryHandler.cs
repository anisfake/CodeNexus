using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.AISummaries.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.AISummaries.Queries.GetResourceSummaries;

public class GetResourceSummariesQueryHandler : IRequestHandler<GetResourceSummariesQuery, Result<List<ResourceSummaryDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetResourceSummariesQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<List<ResourceSummaryDto>>> Handle(GetResourceSummariesQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();

        var resource = await _context.Resources
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.ResourceId == request.ResourceId && !r.IsDeleted, cancellationToken);

        if (resource == null)
        {
            return Result<List<ResourceSummaryDto>>.Failure("RESOURCE_NOT_FOUND", "Resource not found.");
        }

        if (resource.UserId != userId)
        {
            return Result<List<ResourceSummaryDto>>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        var summaries = await _context.AISummaries
            .AsNoTracking()
            .Where(s => s.ResourceId == request.ResourceId && !s.IsDeleted)
            .OrderByDescending(s => s.GeneratedAt)
            .Select(s => new ResourceSummaryDto(
                s.SummaryId,
                s.ResourceId,
                s.Title,
                s.Summary ?? string.Empty,
                s.StartPage,
                s.EndPage
            ))
            .ToListAsync(cancellationToken);

        return Result<List<ResourceSummaryDto>>.Success(summaries);
    }
}
