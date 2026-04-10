using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.FocusSessions.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.FocusSessions.Commands.HeartbeatSession;

public record HeartbeatSessionCommand(Guid SessionId) : IRequest<Result<SessionHeartbeatResponseDto>>;
