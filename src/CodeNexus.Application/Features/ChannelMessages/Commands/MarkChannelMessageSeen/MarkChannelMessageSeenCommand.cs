using CodeNexus.Application.Common.Models;
using MediatR;

namespace CodeNexus.Application.Features.ChannelMessages.Commands.MarkChannelMessageSeen;

public record MarkChannelMessageSeenCommand(Guid MessageId) : IRequest<Result>;
