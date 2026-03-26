using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.ChannelMessages.DTOs;

public record ChannelMessageDto(
    Guid MessageId,
    Guid ConversationId,
    Guid SubjectId,
    SubjectCategory Category,
    Guid SenderId,
    string SenderName,
    string Content,
    DirectMessageType MessageType,
    DateTime SentAt,
    DateTime? DeliveredAt,
    DateTime? SeenAt,
    Guid? LearningPathShareId,
    Guid? ReplyToMessageId,
    string? ReplyToContent,
    Guid? ReplyToSenderId
);
