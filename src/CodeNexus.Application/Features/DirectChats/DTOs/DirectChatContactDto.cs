namespace CodeNexus.Application.Features.DirectChats.DTOs;

public record DirectChatContactDto(
    Guid UserId,
    string Username,
    string? AvatarUrl,
    string RoleName,
    Guid? ConversationId,
    DateTime? LastMessageAt
);
