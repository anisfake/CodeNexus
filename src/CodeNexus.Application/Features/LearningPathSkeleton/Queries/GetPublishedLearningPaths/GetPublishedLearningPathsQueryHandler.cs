using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Queries.GetPublishedLearningPaths;

public class GetPublishedLearningPathsQueryHandler : IRequestHandler<GetPublishedLearningPathsQuery, Result<PaginationDto<PublishedLearningPathSummaryDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetPublishedLearningPathsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PaginationDto<PublishedLearningPathSummaryDto>>> Handle(GetPublishedLearningPathsQuery request, CancellationToken cancellationToken)
    {
        Guid studentId;
        try
        {
            studentId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<PaginationDto<PublishedLearningPathSummaryDto>>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        var query = _context.LearningPaths
            .AsNoTracking()
            .Include(lp => lp.Subject)
            .Include(lp => lp.User)
            .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                .ThenInclude(c => c.Lessons.Where(l => !l.IsDeleted))
            .Where(lp => lp.Status == LearningPathStatus.Published.ToString())
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchTerm = request.SearchTerm.ToLower();
            query = query.Where(lp =>
                lp.Title.ToLower().Contains(searchTerm) ||
                (lp.Description != null && lp.Description.ToLower().Contains(searchTerm)));
        }

        if (request.SubjectId.HasValue)
        {
            query = query.Where(lp => lp.SubjectId == request.SubjectId.Value);
        }

        if (request.ComplexityLevel.HasValue)
        {
            query = query.Where(lp => lp.ComplexityLevel == request.ComplexityLevel.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        query = request.SortDescending
            ? query.OrderByDescending(lp => lp.CreatedAt)
            : query.OrderBy(lp => lp.CreatedAt);

        var paths = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var pathIds = paths.Select(p => p.PathId).ToList();
        var enrolledPathIds = await _context.LearningPathShares
            .AsNoTracking()
            .Where(s => pathIds.Contains(s.PathId)
                        && s.StudentId == studentId
                        && s.Status == LearningPathShareStatus.Accepted)
            .Select(s => s.PathId)
            .ToListAsync(cancellationToken);

        var enrolledSet = new HashSet<Guid>(enrolledPathIds);

        var items = paths.Select(lp => new PublishedLearningPathSummaryDto(
            lp.PathId,
            lp.Title,
            lp.Description,
            lp.SubjectId,
            lp.Subject?.Name ?? string.Empty,
            lp.ComplexityLevel,
            lp.Language,
            lp.VersionNumber,
            lp.UserId,
            lp.User?.Username ?? string.Empty,
            lp.Chapters.Count(c => !c.IsDeleted),
            lp.Chapters.Where(c => !c.IsDeleted).Sum(c => c.Lessons.Count(l => !l.IsDeleted)),
            lp.StartDate,
            lp.EndDate,
            enrolledSet.Contains(lp.PathId)
        )).ToList();

        return Result<PaginationDto<PublishedLearningPathSummaryDto>>.Success(new PaginationDto<PublishedLearningPathSummaryDto>
        {
            Items = items,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalCount = totalCount
        });
    }
}
