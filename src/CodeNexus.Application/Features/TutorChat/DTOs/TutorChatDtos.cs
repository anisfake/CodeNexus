namespace CodeNexus.Application.Features.TutorChat.DTOs;

public record TutorChatResponseDto(
    Guid ConversationId,
    Guid UserMessageId,
    Guid AssistantMessageId,
    string AssistantMessage,
    DateTime CreatedAt
);
