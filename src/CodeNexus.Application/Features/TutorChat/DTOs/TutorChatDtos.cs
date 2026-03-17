namespace CodeNexus.Application.Features.TutorChat.DTOs;

public record TutorChatResponseDto(
    Guid ConversationId,
    Guid UserMessageId,
    Guid AssistantMessageId,
    string AssistantMessage,
    DateTime CreatedAt
);

public record TutorMessageDto(
    Guid MessageId,
    Guid ConversationId,
    string Role,
    string Content,
    DateTime CreatedAt
);
