using CodeNexus.Application.Common.Models;
using MediatR;

namespace CodeNexus.Application.Features.ChannelMessages.Commands.MarkChannelMessageDelivered;

public record MarkChannelMessageDeliveredCommand(Guid MessageId) : IRequest<Result>;
