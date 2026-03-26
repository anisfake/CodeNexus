using CodeNexus.Application.Features.ChannelMessages.Commands.MarkChannelMessageDelivered;
using CodeNexus.Application.Features.ChannelMessages.Commands.MarkChannelMessageSeen;
using CodeNexus.Application.Features.ChannelMessages.Commands.SendChannelMessage;
using CodeNexus.Application.Features.ChannelMessages.Queries.GetChannelMessages;
using CodeNexus.Application.Features.ChannelMessages.Queries.GetChannels;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CodeNexus.API.Hubs;

[Authorize]
public class ChannelChatHub : Hub
{
    private readonly ISender _sender;

    public ChannelChatHub(ISender sender)
    {
        _sender = sender;
    }

    public async Task RequestChannels(Guid subjectId)
    {
        var result = await _sender.Send(new GetChannelsQuery(subjectId));

        if (!result.IsSuccess)
        {
            await Clients.Caller.SendAsync("ChannelChatError", new
            {
                result.ErrorCode,
                result.ErrorMessage
            });
            return;
        }

        await Clients.Caller.SendAsync("ChannelsLoaded", result.Value);
    }

    public async Task JoinChannel(Guid subjectId, string category, int pageNumber = 1, int pageSize = 30)
    {
        if (!Enum.TryParse<SubjectCategory>(category, true, out var parsedCategory))
        {
            await Clients.Caller.SendAsync("ChannelChatError", new
            {
                ErrorCode = "INVALID_CATEGORY",
                ErrorMessage = "Category is invalid."
            });
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GetChannelGroup(subjectId, parsedCategory));

        var messagesResult = await _sender.Send(new GetChannelMessagesQuery(subjectId, parsedCategory, pageNumber, pageSize));

        if (!messagesResult.IsSuccess)
        {
            await Clients.Caller.SendAsync("ChannelChatError", new
            {
                messagesResult.ErrorCode,
                messagesResult.ErrorMessage
            });
            return;
        }

        await Clients.Caller.SendAsync("ChannelMessagesLoaded", messagesResult.Value);
    }

    public async Task LeaveChannel(Guid subjectId, string category)
    {
        if (!Enum.TryParse<SubjectCategory>(category, true, out var parsedCategory))
        {
            return;
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetChannelGroup(subjectId, parsedCategory));
    }

    public async Task RequestChannelMessages(Guid subjectId, string category, int pageNumber = 1, int pageSize = 30)
    {
        if (!Enum.TryParse<SubjectCategory>(category, true, out var parsedCategory))
        {
            await Clients.Caller.SendAsync("ChannelChatError", new
            {
                ErrorCode = "INVALID_CATEGORY",
                ErrorMessage = "Category is invalid."
            });
            return;
        }

        var result = await _sender.Send(new GetChannelMessagesQuery(subjectId, parsedCategory, pageNumber, pageSize));

        if (!result.IsSuccess)
        {
            await Clients.Caller.SendAsync("ChannelChatError", new
            {
                result.ErrorCode,
                result.ErrorMessage
            });
            return;
        }

        await Clients.Caller.SendAsync("ChannelMessagesLoaded", result.Value);
    }

    public async Task SendMessage(Guid subjectId, string category, string content, string messageType = "Text", Guid? replyToMessageId = null)
    {
        if (!Enum.TryParse<SubjectCategory>(category, true, out var parsedCategory))
        {
            await Clients.Caller.SendAsync("ChannelChatError", new
            {
                ErrorCode = "INVALID_CATEGORY",
                ErrorMessage = "Category is invalid."
            });
            return;
        }

        if (!Enum.TryParse<DirectMessageType>(messageType, true, out var parsedMessageType))
        {
            await Clients.Caller.SendAsync("ChannelChatError", new
            {
                ErrorCode = "INVALID_MESSAGE_TYPE",
                ErrorMessage = "Message type is invalid."
            });
            return;
        }

        var result = await _sender.Send(new SendChannelMessageCommand(subjectId, parsedCategory, content, parsedMessageType, replyToMessageId));

        if (!result.IsSuccess)
        {
            await Clients.Caller.SendAsync("ChannelChatError", new
            {
                result.ErrorCode,
                result.ErrorMessage
            });
            return;
        }

        await Clients.Group(GetChannelGroup(subjectId, parsedCategory)).SendAsync("ReceiveChannelMessage", result.Value);
    }

    public async Task MarkDelivered(Guid subjectId, string category, Guid messageId)
    {
        if (!Enum.TryParse<SubjectCategory>(category, true, out var parsedCategory))
        {
            await Clients.Caller.SendAsync("ChannelChatError", new
            {
                ErrorCode = "INVALID_CATEGORY",
                ErrorMessage = "Category is invalid."
            });
            return;
        }

        var result = await _sender.Send(new MarkChannelMessageDeliveredCommand(messageId));

        if (!result.IsSuccess)
        {
            await Clients.Caller.SendAsync("ChannelChatError", new
            {
                result.ErrorCode,
                result.ErrorMessage
            });
            return;
        }

        await Clients.Group(GetChannelGroup(subjectId, parsedCategory)).SendAsync("ChannelMessageDelivered", new
        {
            MessageId = messageId,
            DeliveredAt = DateTime.UtcNow
        });
    }

    public async Task MarkSeen(Guid subjectId, string category, Guid messageId)
    {
        if (!Enum.TryParse<SubjectCategory>(category, true, out var parsedCategory))
        {
            await Clients.Caller.SendAsync("ChannelChatError", new
            {
                ErrorCode = "INVALID_CATEGORY",
                ErrorMessage = "Category is invalid."
            });
            return;
        }

        var result = await _sender.Send(new MarkChannelMessageSeenCommand(messageId));

        if (!result.IsSuccess)
        {
            await Clients.Caller.SendAsync("ChannelChatError", new
            {
                result.ErrorCode,
                result.ErrorMessage
            });
            return;
        }

        await Clients.Group(GetChannelGroup(subjectId, parsedCategory)).SendAsync("ChannelMessageSeen", new
        {
            MessageId = messageId,
            SeenAt = DateTime.UtcNow
        });
    }

    private static string GetChannelGroup(Guid subjectId, SubjectCategory category)
        => $"channel:{subjectId}:{category}";
}
