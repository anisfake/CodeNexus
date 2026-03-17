using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.DirectChats.DTOs;

public record DirectMessageDto(
    Guid MessageId,
    Guid ConversationId,
    Guid SenderId,
    string Content,
    DirectMessageType MessageType,
    DateTime SentAt,
    DateTime? DeliveredAt,
    DateTime? SeenAt
);
