using CodeNexus.Application.Common.Models;
using MediatR;

namespace CodeNexus.Application.Features.DirectChats.Commands.MarkMessageSeen;

public record MarkMessageSeenCommand(Guid MessageId) : IRequest<Result>;
