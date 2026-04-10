using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.FocusSessions.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.FocusSessions.Commands.ResumeSession;

public class ResumeSessionCommandHandler : IRequestHandler<ResumeSessionCommand, Result<ResumeSessionResponseDto>>
{
    private readonly IApplicationDbContext _context;

    public ResumeSessionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ResumeSessionResponseDto>> Handle(ResumeSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _context.FocusSessions
            .FirstOrDefaultAsync(fs => fs.SessionId == request.SessionId, cancellationToken);

        if (session == null)
        {
            return Result<ResumeSessionResponseDto>.Failure("SESSION_NOT_FOUND", "Session not found");
        }

        if (session.SessionStatus != SessionStatus.Paused)
        {
            return Result<ResumeSessionResponseDto>.Failure("SESSION_NOT_PAUSED", "Session is not paused");
        }

        var now = DateTime.UtcNow;
        if (session.PausedAt.HasValue)
        {
            var pausedMinutes = (int)(now - session.PausedAt.Value).TotalMinutes;
            if (pausedMinutes > 0)
            {
                session.TotalPausedMinutes += pausedMinutes;
            }
        }

        session.PausedAt = null;
        session.SessionStatus = SessionStatus.Running;
        session.LastActivityAt = now;

        await _context.SaveChangesAsync(cancellationToken);
        var elapsedSeconds = CalculateElapsedSeconds(session, now);
        var elapsedMinutes = elapsedSeconds / 60;
        var remainingSeconds = session.PlannedDurationMinutes * 60 - elapsedSeconds;
        var remainingMinutes = remainingSeconds / 60;
        var isOvertime = session.PlannedDurationMinutes > 0 &&
                         elapsedSeconds > session.PlannedDurationMinutes * 60;

        var response = new ResumeSessionResponseDto(
            session.SessionId,
            session.SessionStatus.ToString(),
            elapsedMinutes,
            remainingMinutes,
            isOvertime,
            elapsedSeconds,
            remainingSeconds);

        return Result<ResumeSessionResponseDto>.Success(response);
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
