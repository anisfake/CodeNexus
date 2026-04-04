using CodeNexus.Application.Features.ChannelMessages.Commands.MarkChannelMessageDelivered;
using CodeNexus.Application.Features.ChannelMessages.Commands.MarkChannelMessageSeen;
using CodeNexus.Application.Features.ChannelMessages.Commands.SendChannelMessage;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace CodeNexus.API.Hubs;

[Authorize]
public class ChannelChatHub : Hub
{
    private readonly ISender _sender;
    private readonly ILogger<ChannelChatHub> _logger;
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> GroupMembers = new();

    public ChannelChatHub(ISender sender, ILogger<ChannelChatHub> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public override Task OnConnectedAsync()
    {
        _logger.LogInformation("Channel hub connected. UserId={UserId}, ConnectionId={ConnectionId}", Context.UserIdentifier, Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var kvp in GroupMembers)
        {
            if (kvp.Value.TryRemove(Context.ConnectionId, out _))
            {
                _logger.LogInformation("Connection removed from group on disconnect. ConnectionId={ConnectionId}, Group={Group}, GroupCount={GroupCount}", Context.ConnectionId, kvp.Key, kvp.Value.Count);
            }
        }

        await base.OnDisconnectedAsync(exception);
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

        var groupName = GetChannelGroup(parsedCategory);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        var members = GroupMembers.GetOrAdd(groupName, _ => new ConcurrentDictionary<string, byte>());
        members[Context.ConnectionId] = 0;

        _logger.LogInformation("JoinChannel succeeded. UserId={UserId}, ConnectionId={ConnectionId}, Category={Category}, Group={Group}, GroupCount={GroupCount}",
            Context.UserIdentifier,
            Context.ConnectionId,
            parsedCategory,
            groupName,
            members.Count);
    }

    public async Task LeaveChannel(string category)
    {
        if (!Enum.TryParse<SubjectCategory>(category, true, out var parsedCategory))
        {
            return;
        }

        var groupName = GetChannelGroup(parsedCategory);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        if (GroupMembers.TryGetValue(groupName, out var members))
        {
            members.TryRemove(Context.ConnectionId, out _);
            _logger.LogInformation("LeaveChannel succeeded. UserId={UserId}, ConnectionId={ConnectionId}, Category={Category}, Group={Group}, GroupCount={GroupCount}",
                Context.UserIdentifier,
                Context.ConnectionId,
                parsedCategory,
                groupName,
                members.Count);
        }
    }

    public async Task SendMessage(string category, string content, string messageType = "Text", Guid? replyToMessageId = null, Guid? learningPathShareId = null)
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

        _logger.LogInformation("SendMessage called. UserId={UserId}, ConnectionId={ConnectionId}, Category={Category}, MessageType={MessageType}",
            Context.UserIdentifier,
            Context.ConnectionId,
            parsedCategory,
            parsedMessageType);

        var result = await _sender.Send(new SendChannelMessageCommand(parsedCategory, content, parsedMessageType, replyToMessageId, learningPathShareId));

        if (!result.IsSuccess)
        {
            await Clients.Caller.SendAsync("ChannelChatError", new
            {
                result.ErrorCode,
                result.ErrorMessage
            });
            return;
        }

        var groupName = GetChannelGroup(parsedCategory);
        var groupCount = GroupMembers.TryGetValue(groupName, out var members) ? members.Count : 0;

        _logger.LogInformation("SendMessage saved and broadcasting. Category={Category}, Group={Group}, GroupCount={GroupCount}, MessageId={MessageId}",
            parsedCategory,
            groupName,
            groupCount,
            result.Value?.MessageId);

        await Clients.Group(groupName).SendAsync("ReceiveChannelMessage", result.Value);
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

        var groupName = GetChannelGroup(parsedCategory);
        var groupCount = GroupMembers.TryGetValue(groupName, out var members) ? members.Count : 0;
        _logger.LogInformation("MarkDelivered broadcasting. Category={Category}, Group={Group}, GroupCount={GroupCount}, MessageId={MessageId}",
            parsedCategory,
            groupName,
            groupCount,
            messageId);

        await Clients.Group(groupName).SendAsync("ChannelMessageDelivered", new
        {
            Category = parsedCategory.ToString(),
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

        var groupName = GetChannelGroup(parsedCategory);
        var groupCount = GroupMembers.TryGetValue(groupName, out var members) ? members.Count : 0;
        _logger.LogInformation("MarkSeen broadcasting. Category={Category}, Group={Group}, GroupCount={GroupCount}, MessageId={MessageId}",
            parsedCategory,
            groupName,
            groupCount,
            messageId);

        await Clients.Group(groupName).SendAsync("ChannelMessageSeen", new
        {
            Category = parsedCategory.ToString(),
            MessageId = messageId,
            SeenAt = DateTime.UtcNow
        });
    }

    private static string GetChannelGroup(SubjectCategory category)
        => category.ToString();
}
