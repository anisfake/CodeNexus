using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.FocusSessions.Commands.ResumeSession;

public class ResumeSessionCommandHandler : IRequestHandler<ResumeSessionCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public ResumeSessionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(ResumeSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _context.FocusSessions
            .FirstOrDefaultAsync(fs => fs.SessionId == request.SessionId, cancellationToken);

        if (session == null)
        {
            return Result.Failure("SESSION_NOT_FOUND", "Session not found");
        }

        if (session.SessionStatus != SessionStatus.Paused)
        {
            return Result.Failure("SESSION_NOT_PAUSED", "Session is not paused");
        }

        if (session.PausedAt.HasValue)
        {
            var pausedMinutes = (int)(DateTime.UtcNow - session.PausedAt.Value).TotalMinutes;
            if (pausedMinutes > 0)
            {
                session.TotalPausedMinutes += pausedMinutes;
            }
        }

        session.PausedAt = null;
        session.SessionStatus = SessionStatus.Running;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
