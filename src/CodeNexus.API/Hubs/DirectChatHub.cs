using CodeNexus.Application.Features.DirectChats.Commands.CreateDirectConversation;
using CodeNexus.Application.Features.DirectChats.Commands.MarkMessageDelivered;
using CodeNexus.Application.Features.DirectChats.Commands.MarkMessageSeen;
using CodeNexus.Application.Features.DirectChats.Commands.SendDirectMessage;
using CodeNexus.Application.Features.DirectChats.Queries.GetDirectChatContacts;
using CodeNexus.Application.Features.DirectChats.Queries.GetConversationMessages;
using CodeNexus.Application.Features.DirectChats.Queries.GetConversations;
using CodeNexus.Application.Features.DirectChats.Queries.GetUnreadCount;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CodeNexus.API.Hubs;

[Authorize]
public class DirectChatHub : Hub
{
    private readonly ISender _sender;

    public DirectChatHub(ISender sender)
    {
        _sender = sender;
    }

    public async Task JoinConversation(Guid conversationId, int pageNumber = 1, int pageSize = 30)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GetConversationGroup(conversationId));

        var messagesResult = await _sender.Send(new GetConversationMessagesQuery(conversationId, pageNumber, pageSize));

        if (!messagesResult.IsSuccess)
        {
            await Clients.Caller.SendAsync("DirectChatError", new
            {
                messagesResult.ErrorCode,
                messagesResult.ErrorMessage
            });
            return;
        }

        await Clients.Caller.SendAsync("ConversationMessagesLoaded", messagesResult.Value);
    }

    public async Task StartConversation(Guid participantId)
    {
        var result = await _sender.Send(new CreateDirectConversationCommand(participantId));

        if (!result.IsSuccess)
        {
            await Clients.Caller.SendAsync("DirectChatError", new
            {
                result.ErrorCode,
                result.ErrorMessage
            });
            return;
        }

        await Clients.Caller.SendAsync("ConversationStarted", result.Value);
    }

    public async Task LeaveConversation(Guid conversationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetConversationGroup(conversationId));
    }

    public async Task RequestConversations()
    {
        var result = await _sender.Send(new GetConversationsQuery());

        if (!result.IsSuccess)
        {
            await Clients.Caller.SendAsync("DirectChatError", new
            {
                result.ErrorCode,
                result.ErrorMessage
            });
            return;
        }

        await Clients.Caller.SendAsync("ConversationsLoaded", result.Value);
    }

    public async Task RequestChatContacts()
    {
        var result = await _sender.Send(new GetDirectChatContactsQuery());

        if (!result.IsSuccess)
        {
            await Clients.Caller.SendAsync("DirectChatError", new
            {
                result.ErrorCode,
                result.ErrorMessage
            });
            return;
        }

        await Clients.Caller.SendAsync("ChatContactsLoaded", result.Value);
    }

    public async Task RequestUnreadCount()
    {
        var result = await _sender.Send(new GetUnreadCountQuery());

        if (!result.IsSuccess)
        {
            await Clients.Caller.SendAsync("DirectChatError", new
            {
                result.ErrorCode,
                result.ErrorMessage
            });
            return;
        }

        await Clients.Caller.SendAsync("UnreadCountUpdated", result.Value);
    }

    public async Task SendMessage(Guid conversationId, string content, string messageType = "Text", Guid? replyToMessageId = null)
    {
        if (!Enum.TryParse<DirectMessageType>(messageType, true, out var parsedMessageType))
        {
            await Clients.Caller.SendAsync("DirectChatError", new
            {
                ErrorCode = "INVALID_MESSAGE_TYPE",
                ErrorMessage = "Message type is invalid."
            });
            return;
        }

        var result = await _sender.Send(new SendDirectMessageCommand(conversationId, content, parsedMessageType, replyToMessageId));

        if (!result.IsSuccess)
        {
            await Clients.Caller.SendAsync("DirectChatError", new
            {
                result.ErrorCode,
                result.ErrorMessage
            });
            return;
        }

        await Clients.Group(GetConversationGroup(conversationId)).SendAsync("ReceiveMessage", result.Value);

        var conversationsResult = await _sender.Send(new GetConversationsQuery());
        if (conversationsResult.IsSuccess)
        {
            var updatedConversation = conversationsResult.Value?.FirstOrDefault(c => c.ConversationId == conversationId);
            if (updatedConversation != null)
            {
                await Clients.Caller.SendAsync("ConversationUpdated", updatedConversation);

                if (Guid.TryParse(Context.UserIdentifier, out var currentUserId))
                {
                    var recipientId = currentUserId == updatedConversation.MentorId
                        ? updatedConversation.StudentId
                        : updatedConversation.MentorId;

                    await Clients.User(recipientId.ToString()).SendAsync("ConversationUpdated", new
                    {
                        updatedConversation.ConversationId,
                        updatedConversation.LastMessagePreview,
                        updatedConversation.LastMessageAt
                    });

                    await Clients.User(recipientId.ToString()).SendAsync("UnreadCountUpdated", new
                    {
                        updatedConversation.ConversationId
                    });

                    await Clients.User(recipientId.ToString()).SendAsync("NewMessageNotification", new
                    {
                        ConversationId = updatedConversation.ConversationId,
                        MessageId = result.Value?.MessageId,
                        Preview = updatedConversation.LastMessagePreview,
                        SentAt = updatedConversation.LastMessageAt,
                        BadgeIncrement = 1,
                        PlaySound = true
                    });
                }
            }
        }

        await RequestUnreadCount();
    }

    public async Task MarkDelivered(Guid conversationId, Guid messageId)
    {
        var result = await _sender.Send(new MarkMessageDeliveredCommand(messageId));

        if (!result.IsSuccess)
        {
            await Clients.Caller.SendAsync("DirectChatError", new
            {
                result.ErrorCode,
                result.ErrorMessage
            });
            return;
        }

        await Clients.Group(GetConversationGroup(conversationId)).SendAsync("MessageDelivered", new
        {
            MessageId = messageId,
            DeliveredAt = DateTime.UtcNow
        });
    }

    public async Task MarkSeen(Guid conversationId, Guid messageId)
    {
        var result = await _sender.Send(new MarkMessageSeenCommand(messageId));

        if (!result.IsSuccess)
        {
            await Clients.Caller.SendAsync("DirectChatError", new
            {
                result.ErrorCode,
                result.ErrorMessage
            });
            return;
        }

        await Clients.Group(GetConversationGroup(conversationId)).SendAsync("MessageSeen", new
        {
            MessageId = messageId,
            SeenAt = DateTime.UtcNow
        });

        await RequestUnreadCount();
    }

    private static string GetConversationGroup(Guid conversationId)
        => $"direct-conversation:{conversationId}";
}
