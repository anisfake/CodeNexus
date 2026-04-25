using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.TaskReviews.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.TaskReviews.Queries.GetTaskReviews;

public class GetTaskReviewsQueryHandler : IRequestHandler<GetTaskReviewsQuery, Result<PaginationDto<TaskReviewListItemDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetTaskReviewsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PaginationDto<TaskReviewListItemDto>>> Handle(
        GetTaskReviewsQuery request,
        CancellationToken cancellationToken)
    {
        Guid userId;
        try
        {
            userId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<PaginationDto<TaskReviewListItemDto>>.Failure("UNAUTHORIZED", "User not authenticated.");
        }

        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize <= 0 ? 20 : Math.Min(request.PageSize, 50);

        // Return reviews where the caller is either the mentor or the student
        var query = _context.TaskReviews
            .AsNoTracking()
            .Where(r => r.MentorId == userId || r.StudentId == userId);

        // Status filter – default Pending when null/empty
        var statusString = string.IsNullOrWhiteSpace(request.Status) ? nameof(TaskReviewStatus.Pending) : request.Status.Trim();
        if (!Enum.TryParse<TaskReviewStatus>(statusString, ignoreCase: true, out var statusEnum))
            statusEnum = TaskReviewStatus.Pending;
        query = query.Where(r => r.Status == statusEnum);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.RequestedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new TaskReviewListItemDto(
                r.ReviewId,
                r.SessionId,
                r.TaskId,
                r.Task.Title,
                r.StudentId,
                r.Student.Username,
                r.Student.UserProfile != null ? r.Student.UserProfile.AvatarUrl : null,
                r.MentorId,
                r.Mentor.Username,
                r.Mentor.UserProfile != null ? r.Mentor.UserProfile.AvatarUrl : null,
                r.Score,
                r.StudentRequestNote,
                r.Status.ToString(),
                r.RequestedAt,
                r.ReviewedAt
            ))
            .ToListAsync(cancellationToken);

        return Result<PaginationDto<TaskReviewListItemDto>>.Success(new PaginationDto<TaskReviewListItemDto>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }
}
