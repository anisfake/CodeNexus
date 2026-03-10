using CodeNexus.Application.Common.Models;
using MediatR;

namespace CodeNexus.Application.Features.FocusSessions.Commands.AbandonSession;

public record AbandonSessionCommand(Guid SessionId) : IRequest<Result>;