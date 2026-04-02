using CodeNexus.API.Hubs;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.DirectChats.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace CodeNexus.API.Services;

public class LearningPathShareRealtimeNotifier : ILearningPathShareRealtimeNotifier
{
    private readonly IHubContext<DirectChatHub> _hubContext;

    public LearningPathShareRealtimeNotifier(IHubContext<DirectChatHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyShareSentAsync(
        Guid recipientUserId,
        Guid conversationId,
        string? lastMessagePreview,
        DateTime? lastMessageAt,
        DirectMessageDto message,
        CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group($"direct-conversation:{conversationId}")
            .SendAsync("ReceiveMessage", message, cancellationToken);

        await _hubContext.Clients.User(recipientUserId.ToString())
            .SendAsync("ConversationUpdated", new
            {
                ConversationId = conversationId,
                LastMessagePreview = lastMessagePreview,
                LastMessageAt = lastMessageAt
            }, cancellationToken);

        await _hubContext.Clients.User(recipientUserId.ToString())
            .SendAsync("UnreadCountUpdated", new
            {
                ConversationId = conversationId
            }, cancellationToken);

        await _hubContext.Clients.User(recipientUserId.ToString())
            .SendAsync("NewMessageNotification", new
            {
                ConversationId = conversationId,
                MessageId = message.MessageId,
                Preview = lastMessagePreview,
                SentAt = lastMessageAt,
                BadgeIncrement = 1,
                PlaySound = true
            }, cancellationToken);
    }
}
