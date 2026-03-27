using CodeNexus.API.Models.Requests;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.ChannelMessages.Commands.MarkChannelMessageDelivered;
using CodeNexus.Application.Features.ChannelMessages.Commands.MarkChannelMessageSeen;
using CodeNexus.Application.Features.ChannelMessages.Commands.SendChannelMessage;
using CodeNexus.Application.Features.ChannelMessages.Queries.GetChannelMessages;
using CodeNexus.Application.Features.ChannelMessages.Queries.GetChannels;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using CodeNexus.API.Hubs;

namespace CodeNexus.API.Controllers;

[ApiController]
[Route("api/channel-messages")]
[Authorize]
public class ChannelMessageController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IHubContext<ChannelChatHub> _hubContext;

    public ChannelMessageController(ISender sender, IHubContext<ChannelChatHub> hubContext)
    {
        _sender = sender;
        _hubContext = hubContext;
    }

    [HttpGet("channels")]
    public async Task<IActionResult> GetChannels(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetChannelsQuery(), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("channels/{category}/messages")]
    public async Task<IActionResult> GetMessages(
        SubjectCategory category,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 30,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new GetChannelMessagesQuery(category, pageNumber, pageSize),
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpPost("channels/{category}/messages")]
    public async Task<IActionResult> SendMessage(
        SubjectCategory category,
        [FromBody] SendChannelMessageRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new SendChannelMessageCommand(category, request.Content, request.MessageType, request.ReplyToMessageId, request.LearningPathShareId),
            cancellationToken);

        if (result.IsSuccess && result.Value is not null)
        {
            await _hubContext.Clients.Group(GetChannelGroup(category))
                .SendAsync("ReceiveChannelMessage", result.Value, cancellationToken);
        }

        return ToActionResult(result);
    }

    [HttpPatch("messages/{messageId:guid}/delivered")]
    public async Task<IActionResult> MarkDelivered(Guid messageId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new MarkChannelMessageDeliveredCommand(messageId), cancellationToken);

        if (result.IsSuccess)
        {
            await _hubContext.Clients.All.SendAsync("ChannelMessageDelivered", new
            {
                MessageId = messageId,
                DeliveredAt = DateTime.UtcNow
            }, cancellationToken);
        }

        return ToActionResult(result);
    }

    [HttpPatch("messages/{messageId:guid}/seen")]
    public async Task<IActionResult> MarkSeen(Guid messageId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new MarkChannelMessageSeenCommand(messageId), cancellationToken);

        if (result.IsSuccess)
        {
            await _hubContext.Clients.All.SendAsync("ChannelMessageSeen", new
            {
                MessageId = messageId,
                SeenAt = DateTime.UtcNow
            }, cancellationToken);
        }

        return ToActionResult(result);
    }

    private static string GetChannelGroup(SubjectCategory category)
        => category.ToString();

    private IActionResult ToActionResult(Result result)
    {
        if (result.IsSuccess)
            return Ok(result);

        return result.ErrorCode switch
        {
            "UNAUTHORIZED" => Unauthorized(new { result.ErrorCode, result.ErrorMessage }),
            "ACCESS_DENIED" => StatusCode(StatusCodes.Status403Forbidden, new { result.ErrorCode, result.ErrorMessage }),
            "MESSAGE_NOT_FOUND" or "LEARNING_PATH_SHARE_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
            _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
        };
    }

    private IActionResult ToActionResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return Ok(result.Value);

        return result.ErrorCode switch
        {
            "UNAUTHORIZED" => Unauthorized(new { result.ErrorCode, result.ErrorMessage }),
            "ACCESS_DENIED" => StatusCode(StatusCodes.Status403Forbidden, new { result.ErrorCode, result.ErrorMessage }),
            "MESSAGE_NOT_FOUND" or "LEARNING_PATH_SHARE_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
            _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
        };
    }
}
