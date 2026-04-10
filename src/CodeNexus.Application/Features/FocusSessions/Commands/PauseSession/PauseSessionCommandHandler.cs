using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.FocusSessions.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.FocusSessions.Commands.PauseSession;

public class PauseSessionCommandHandler : IRequestHandler<PauseSessionCommand, Result<PauseSessionResponseDto>>
{
    private readonly IApplicationDbContext _context;

    public PauseSessionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PauseSessionResponseDto>> Handle(PauseSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _context.FocusSessions
            .FirstOrDefaultAsync(fs => fs.SessionId == request.SessionId, cancellationToken);

        if (session == null)
        {
            return Result<PauseSessionResponseDto>.Failure("SESSION_NOT_FOUND", "Session not found");
        }

        if (session.SessionStatus != SessionStatus.Running)
        {
            return Result<PauseSessionResponseDto>.Failure("SESSION_NOT_RUNNING", "Session is not running");
        }

        var now = DateTime.UtcNow;
        session.SessionStatus = SessionStatus.Paused;
        session.PausedAt = now;
        session.LastActivityAt = now;

        await _context.SaveChangesAsync(cancellationToken);
        var elapsedSeconds = CalculateElapsedSeconds(session, now);
        var elapsedMinutes = elapsedSeconds / 60;
        var remainingSeconds = session.PlannedDurationMinutes * 60 - elapsedSeconds;
        var remainingMinutes = remainingSeconds / 60;
        var isOvertime = session.PlannedDurationMinutes > 0 &&
                         elapsedSeconds > session.PlannedDurationMinutes * 60;

        var response = new PauseSessionResponseDto(
            session.SessionId,
            session.SessionStatus.ToString(),
            elapsedMinutes,
            remainingMinutes,
            isOvertime,
            elapsedSeconds,
            remainingSeconds);

        return Result<PauseSessionResponseDto>.Success(response);
    }

    private static int CalculateElapsedSeconds(FocusSession session, DateTime now)
    {
        var pausedSeconds = session.TotalPausedMinutes * 60;
        if (session.PausedAt.HasValue)
        {
            var extra = (int)(now - session.PausedAt.Value).TotalSeconds;
            if (extra > 0)
            {
                pausedSeconds += extra;
            }
        }

        var elapsed = (int)(now - session.StartTime).TotalSeconds - pausedSeconds;
        return Math.Max(0, elapsed);
    }
}
