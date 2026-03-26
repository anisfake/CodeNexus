using CodeNexus.Application.Features.ChannelMessages.Commands.MarkChannelMessageDelivered;
using CodeNexus.Application.Features.ChannelMessages.Commands.MarkChannelMessageSeen;
using CodeNexus.Application.Features.ChannelMessages.Commands.SendChannelMessage;
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

    public async Task JoinChannel(string category)
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

        await Groups.AddToGroupAsync(Context.ConnectionId, GetChannelGroup(parsedCategory));
    }

    public async Task LeaveChannel(string category)
    {
        if (!Enum.TryParse<SubjectCategory>(category, true, out var parsedCategory))
        {
            return;
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetChannelGroup(parsedCategory));
    }

    public async Task SendMessage(string category, string content, string messageType = "Text", Guid? replyToMessageId = null)
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

        var result = await _sender.Send(new SendChannelMessageCommand(parsedCategory, content, parsedMessageType, replyToMessageId));

        if (!result.IsSuccess)
        {
            await Clients.Caller.SendAsync("ChannelChatError", new
            {
                result.ErrorCode,
                result.ErrorMessage
            });
            return;
        }

        await Clients.Group(GetChannelGroup(parsedCategory)).SendAsync("ReceiveChannelMessage", result.Value);
    }

    public async Task MarkDelivered(string category, Guid messageId)
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

        await Clients.Group(GetChannelGroup(parsedCategory)).SendAsync("ChannelMessageDelivered", new
        {
            MessageId = messageId,
            DeliveredAt = DateTime.UtcNow
        });
    }

    public async Task MarkSeen(string category, Guid messageId)
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

        await Clients.Group(GetChannelGroup(parsedCategory)).SendAsync("ChannelMessageSeen", new
        {
            MessageId = messageId,
            SeenAt = DateTime.UtcNow
        });
    }

    private static string GetChannelGroup(SubjectCategory category)
        => $"channel:{category}";
}
