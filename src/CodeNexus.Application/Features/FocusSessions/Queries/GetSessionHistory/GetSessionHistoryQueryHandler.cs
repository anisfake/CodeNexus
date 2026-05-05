using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.FocusSessions.DTOs;
using CodeNexus.Application.Features.TaskReviews.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.FocusSessions.Queries.GetSessionHistory;

public class GetSessionHistoryQueryHandler : IRequestHandler<GetSessionHistoryQuery, Result<PaginationDto<FocusSessionHistoryItemDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetSessionHistoryQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PaginationDto<FocusSessionHistoryItemDto>>> Handle(GetSessionHistoryQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.GetUserId();

            var query = _context.FocusSessions
                .AsNoTracking()
                .Where(fs => fs.Task.LearningPath.UserId == userId);

            if (request.TaskId.HasValue)
            {
                query = query.Where(fs => fs.TaskId == request.TaskId.Value);
            }

            if (request.SessionStatus.HasValue)
            {
                query = query.Where(fs => fs.SessionStatus == request.SessionStatus.Value);
            }
            else
            {
                query = query.Where(fs =>
                    fs.SessionStatus != SessionStatus.Running);
            }

            if (request.SessionType.HasValue)
            {
                query = query.Where(fs => fs.SessionType == request.SessionType.Value);
            }

            if (!request.IncludeAbandoned && request.SessionStatus != SessionStatus.Abandoned)
            {
                query = query.Where(fs => fs.SessionStatus != SessionStatus.Abandoned);
            }

            if (request.StartedFrom.HasValue)
            {
                query = query.Where(fs => fs.StartTime >= request.StartedFrom.Value);
            }

            if (request.StartedTo.HasValue)
            {
                query = query.Where(fs => fs.StartTime <= request.StartedTo.Value);
            }

            var totalCount = await query.CountAsync(cancellationToken);

            var sessions = await query
                .OrderByDescending(fs => fs.StartTime)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(fs => new FocusSessionHistoryItemDto(
                    fs.SessionId,
                    fs.TaskId,
                    fs.Task.Title,
                    fs.Task.ChapterId,
                    fs.Task.Chapter.Title,
                    fs.Task.PathId,
                    fs.Task.LearningPath.Title,
                    fs.Title ?? "Focus Session",
                    fs.StartTime,
                    fs.EndTime,
                    fs.PlannedDurationMinutes,
                    fs.ActualDurationMinutes,
                    fs.SessionStatus.ToString(),
                    fs.SessionType.ToString(),
                    fs.IsVerified,
                    fs.VerificationScore,
                    fs.SubmittedCode,
                    fs.SubmittedSummary,
                    fs.AIFeedback,
                    fs.CreatedAt,
                    fs.TaskReviews
                        .OrderByDescending(r => r.RequestedAt)
                        .Select(r => new TaskReviewInfoDto(
                            r.ReviewId,
                            r.MentorId,
                            r.Mentor.Username,
                            r.Score,
                            r.Feedback,
                            r.Suggestions,
                            r.Status.ToString(),
                            r.RequestedAt,
                            r.ReviewedAt))
                        .FirstOrDefault()
                ))
                .ToListAsync(cancellationToken);

            var pagedResult = new PaginationDto<FocusSessionHistoryItemDto>
            {
                Items = sessions,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                TotalCount = totalCount
            };

            return Result<PaginationDto<FocusSessionHistoryItemDto>>.Success(pagedResult);
        }
        catch (Exception ex)
        {
            return Result<PaginationDto<FocusSessionHistoryItemDto>>.Failure(
                "GET_SESSION_HISTORY_FAILED",
                $"An error occurred while retrieving session history: {ex.Message}");
        }
    }
}
