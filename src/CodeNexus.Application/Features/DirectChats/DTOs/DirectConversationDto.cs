namespace CodeNexus.Application.Features.DirectChats.DTOs;

public record DirectConversationDto(
    Guid ConversationId,
    Guid MentorId,
    string MentorName,
    Guid StudentId,
    string StudentName,
    string? LastMessagePreview,
    DateTime? LastMessageAt,
    int UnreadCount
);
