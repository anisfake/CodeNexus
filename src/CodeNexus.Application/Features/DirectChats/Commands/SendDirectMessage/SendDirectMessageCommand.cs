using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.DirectChats.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;

namespace CodeNexus.Application.Features.DirectChats.Commands.SendDirectMessage;

public record SendDirectMessageCommand(
    Guid ConversationId,
    string Content,
    DirectMessageType MessageType = DirectMessageType.Text
) : IRequest<Result<DirectMessageDto>>;
