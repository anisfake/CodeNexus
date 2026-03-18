using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.FocusSessions.Commands.AbandonSession;

public class AbandonSessionCommandHandler : IRequestHandler<AbandonSessionCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public AbandonSessionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(AbandonSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _context.FocusSessions
            .FirstOrDefaultAsync(fs => fs.SessionId == request.SessionId, cancellationToken);

        if (session == null)
        {
            return Result.Failure("SESSION_NOT_FOUND", "Session not found");
        }

        if (session.SessionStatus != SessionStatus.Running)
        {
            return Result.Failure("SESSION_NOT_ACTIVE", "Session is not active");
        }

        try
        {
            var endTime = DateTime.UtcNow;
            var actualDurationMinutes = (int)(endTime - session.StartTime).TotalMinutes;

            session.EndTime = endTime;
            session.ActualDurationMinutes = actualDurationMinutes;
            session.SessionStatus = SessionStatus.Abandoned;

            await _context.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(
                "ABANDON_SESSION_FAILED",
                $"An error occurred while abandoning the session: {ex.Message}");
        }
    }
}