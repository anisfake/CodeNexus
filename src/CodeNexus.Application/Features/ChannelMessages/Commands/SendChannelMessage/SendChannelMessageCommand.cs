using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.ChannelMessages.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;

namespace CodeNexus.Application.Features.ChannelMessages.Commands.SendChannelMessage;

public record SendChannelMessageCommand(
    SubjectCategory Category,
    string Content,
    DirectMessageType MessageType = DirectMessageType.Text,
    Guid? ReplyToMessageId = null,
    Guid? LearningPathShareId = null
) : IRequest<Result<ChannelMessageDto>>;
