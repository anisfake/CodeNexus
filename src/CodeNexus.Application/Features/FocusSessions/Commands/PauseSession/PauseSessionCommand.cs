using CodeNexus.Application.Common.Models;
using MediatR;

namespace CodeNexus.Application.Features.FocusSessions.Commands.PauseSession;

public record PauseSessionCommand(Guid SessionId) : IRequest<Result>;
