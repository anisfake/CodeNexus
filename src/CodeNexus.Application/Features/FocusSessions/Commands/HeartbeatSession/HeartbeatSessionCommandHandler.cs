using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.FocusSessions.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.FocusSessions.Commands.HeartbeatSession;

public class HeartbeatSessionCommandHandler : IRequestHandler<HeartbeatSessionCommand, Result<SessionHeartbeatResponseDto>>
{
    private readonly IApplicationDbContext _context;

    public HeartbeatSessionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<SessionHeartbeatResponseDto>> Handle(HeartbeatSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _context.FocusSessions
            .FirstOrDefaultAsync(fs => fs.SessionId == request.SessionId, cancellationToken);

        if (session == null)
        {
            return Result<SessionHeartbeatResponseDto>.Failure("SESSION_NOT_FOUND", "Session not found");
        }

        if (session.SessionStatus != SessionStatus.Running && session.SessionStatus != SessionStatus.Paused)
        {
            return Result<SessionHeartbeatResponseDto>.Failure("SESSION_NOT_ACTIVE", "Session is not active");
        }

        var now = DateTime.UtcNow;
        session.LastActivityAt = now;
        await _context.SaveChangesAsync(cancellationToken);

        var response = new SessionHeartbeatResponseDto(
            session.SessionId,
            session.SessionStatus.ToString(),
            session.LastActivityAt);

        return Result<SessionHeartbeatResponseDto>.Success(response);
    }
}
