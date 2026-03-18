using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.FocusSessions.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.FocusSessions.Queries.GetActiveSession;

public class GetActiveSessionQueryHandler : IRequestHandler<GetActiveSessionQuery, Result<ActiveSessionDto?>>
{
    private readonly IApplicationDbContext _context;

    public GetActiveSessionQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ActiveSessionDto?>> Handle(GetActiveSessionQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var activeSession = await _context.FocusSessions
                .Where(fs => fs.TaskId == request.TaskId &&
                            (fs.SessionStatus == SessionStatus.Running ||
                             fs.SessionStatus == SessionStatus.Paused))
                .OrderByDescending(fs => fs.StartTime)
                .FirstOrDefaultAsync(cancellationToken);

            if (activeSession == null)
            {
                return Result<ActiveSessionDto?>.Failure("NO_ACTIVE_SESSION", "No active session found for this task");
            }

            var now = DateTime.UtcNow;
            var elapsedMinutes = CalculateElapsedMinutes(activeSession, now);
            var remainingMinutes = activeSession.PlannedDurationMinutes - elapsedMinutes;
            var isOvertime = activeSession.PlannedDurationMinutes > 0 &&
                             elapsedMinutes > activeSession.PlannedDurationMinutes;

            var activeSessionDto = new ActiveSessionDto(
                activeSession.SessionId,
                activeSession.TaskId,
                activeSession.Title ?? "Focus Session",
                activeSession.StartTime,
                activeSession.PlannedDurationMinutes,
                elapsedMinutes,
                remainingMinutes,
                activeSession.SessionStatus.ToString(),
                isOvertime
            );

            return Result<ActiveSessionDto?>.Success(activeSessionDto);
        }
        catch (Exception ex)
        {
            return Result<ActiveSessionDto?>.Failure(
                "GET_ACTIVE_SESSION_FAILED",
                $"An error occurred while retrieving active session: {ex.Message}");
        }
    }

    private static int CalculateElapsedMinutes(FocusSession session, DateTime now)
    {
        var pausedMinutes = session.TotalPausedMinutes;
        if (session.PausedAt.HasValue)
        {
            var extra = (int)(now - session.PausedAt.Value).TotalMinutes;
            if (extra > 0)
            {
                pausedMinutes += extra;
            }
        }

        var elapsed = (int)(now - session.StartTime).TotalMinutes - pausedMinutes;
        return Math.Max(0, elapsed);
    }
}
