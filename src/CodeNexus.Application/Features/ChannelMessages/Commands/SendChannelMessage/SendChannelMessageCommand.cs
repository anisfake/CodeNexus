using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.ChannelMessages.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;

namespace CodeNexus.Application.Features.ChannelMessages.Commands.SendChannelMessage;

public record SendChannelMessageCommand(
    Guid SubjectId,
    SubjectCategory Category,
    string Content,
    DirectMessageType MessageType = DirectMessageType.Text,
    Guid? ReplyToMessageId = null
) : IRequest<Result<ChannelMessageDto>>;
