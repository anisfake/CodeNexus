using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.FocusSessions.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.FocusSessions.Queries.GetSessionHistory;

public class GetSessionHistoryQueryHandler : IRequestHandler<GetSessionHistoryQuery, Result<List<FocusSessionDto>>>
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

    public async Task<Result<List<FocusSessionDto>>> Handle(GetSessionHistoryQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.GetUserId();

            var query = _context.FocusSessions
                .Include(fs => fs.Task)
                    .ThenInclude(t => t.LearningPath)
                .Where(fs => fs.Task.LearningPath.UserId == userId);

            if (request.TaskId.HasValue)
            {
                query = query.Where(fs => fs.TaskId == request.TaskId.Value);
            }

            var sessions = await query
                .OrderByDescending(fs => fs.CreatedAt)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(fs => new FocusSessionDto(
                    fs.SessionId,
                    fs.TaskId,
                    fs.Title ?? "Focus Session",
                    fs.StartTime,
                    fs.EndTime,
                    fs.PlannedDurationMinutes,
                    fs.ActualDurationMinutes,
                    fs.SessionStatus.ToString(),
                    fs.SessionType.ToString(),
                    fs.SubmittedCode,
                    fs.SubmittedSummary,
                    fs.AIFeedback,
                    fs.VerificationScore,
                    fs.IsVerified,
                    fs.CreatedAt
                ))
                .ToListAsync(cancellationToken);

            return Result<List<FocusSessionDto>>.Success(sessions);
        }
        catch (Exception ex)
        {
            return Result<List<FocusSessionDto>>.Failure(
                "GET_SESSION_HISTORY_FAILED",
                $"An error occurred while retrieving session history: {ex.Message}");
        }
    }
}