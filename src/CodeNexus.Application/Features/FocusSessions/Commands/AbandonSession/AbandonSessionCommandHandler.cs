using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.FocusSessions.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.FocusSessions.Commands.AbandonSession;

public class AbandonSessionCommandHandler : IRequestHandler<AbandonSessionCommand, Result<AbandonSessionResponseDto>>
{
    private readonly IApplicationDbContext _context;

    public AbandonSessionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<AbandonSessionResponseDto>> Handle(AbandonSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _context.FocusSessions
            .Include(fs => fs.Task)
            .FirstOrDefaultAsync(fs => fs.SessionId == request.SessionId, cancellationToken);

        if (session == null)
        {
            return Result<AbandonSessionResponseDto>.Failure("SESSION_NOT_FOUND", "Session not found");
        }

        if (session.SessionStatus != SessionStatus.Running &&
            session.SessionStatus != SessionStatus.Paused)
        {
            return Result<AbandonSessionResponseDto>.Failure("SESSION_NOT_ACTIVE", "Session is not active");
        }

        try
        {
            var endTime = DateTime.UtcNow;
            var actualDurationMinutes = CalculateElapsedMinutes(session, endTime);

            session.EndTime = endTime;
            session.ActualDurationMinutes = actualDurationMinutes;
            session.SessionStatus = SessionStatus.Abandoned;
            session.LastActivityAt = endTime;

            await _context.SaveChangesAsync(cancellationToken);

            var response = new AbandonSessionResponseDto(
                session.SessionId,
                endTime,
                actualDurationMinutes,
                session.SessionStatus.ToString(),
                false,
                "Session abandoned successfully");

            return Result<AbandonSessionResponseDto>.Success(response);
        }
        catch (Exception ex)
        {
            return Result<AbandonSessionResponseDto>.Failure(
                "ABANDON_SESSION_FAILED",
                $"An error occurred while abandoning the session: {ex.Message}");
        }
    }

    private static int CalculateElapsedMinutes(FocusSession session, DateTime now)
    {
        var pausedSeconds = Math.Max(0, session.TotalPausedSeconds);
        if (session.PausedAt.HasValue)
        {
            var extra = ToWholeSeconds(now - session.PausedAt.Value);
            if (extra > 0)
            {
                pausedSeconds += extra;
                session.TotalPausedSeconds = pausedSeconds;
                session.TotalPausedMinutes = pausedSeconds / 60;
            }

            session.PausedAt = null;
        }

        var elapsedSeconds = ToWholeSeconds(now - session.StartTime) - pausedSeconds;
        return Math.Max(0, elapsedSeconds / 60);
    }

    private static int ToWholeSeconds(TimeSpan duration)
        => (int)(duration.Ticks / TimeSpan.TicksPerSecond);
}
