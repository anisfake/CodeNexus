using CodeNexus.Application.Common.Models;

namespace CodeNexus.Application.Features.TutorChat.DTOs;

public record TutorChatResponseDto(
    Guid ConversationId,
    Guid UserMessageId,
    Guid AssistantMessageId,
    string AssistantMessage,
    DateTime CreatedAt,
    double ContextUsagePercent
);

public record TutorMessageDto(
    Guid MessageId,
    Guid ConversationId,
    string Role,
    string Content,
    DateTime CreatedAt
);

public record TutorConversationSummaryDto(
    Guid SummaryId,
    Guid ConversationId,
    string SummaryContent,
    int MessageCount,
    DateTime? StartMessageCreatedAt,
    DateTime? EndMessageCreatedAt,
    DateTime CreatedAt
);

public record ResolveTutorConversationResponseDto(
    Guid ConversationId,
    bool Created
);

public class TutorMessagesPageDto : PaginationDto<TutorMessageDto>
{
    public double ContextUsagePercent { get; set; }
}
