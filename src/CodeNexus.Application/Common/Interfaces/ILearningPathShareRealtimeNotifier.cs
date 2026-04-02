using CodeNexus.Application.Features.DirectChats.DTOs;

namespace CodeNexus.Application.Common.Interfaces;

public interface ILearningPathShareRealtimeNotifier
{
    Task NotifyShareSentAsync(
        Guid recipientUserId,
        Guid conversationId,
        string? lastMessagePreview,
        DateTime? lastMessageAt,
        DirectMessageDto message,
        CancellationToken cancellationToken = default);
}
