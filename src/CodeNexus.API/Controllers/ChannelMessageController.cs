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

namespace CodeNexus.API.Controllers;

[ApiController]
[Route("api/channel-messages")]
[Authorize]
public class ChannelMessageController : ControllerBase
{
    private readonly ISender _sender;

    public ChannelMessageController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("subjects/{subjectId:guid}/channels")]
    public async Task<IActionResult> GetChannels(Guid subjectId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetChannelsQuery(subjectId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("subjects/{subjectId:guid}/channels/{category}/messages")]
    public async Task<IActionResult> GetMessages(
        Guid subjectId,
        SubjectCategory category,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 30,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new GetChannelMessagesQuery(subjectId, category, pageNumber, pageSize),
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpPost("subjects/{subjectId:guid}/channels/{category}/messages")]
    public async Task<IActionResult> SendMessage(
        Guid subjectId,
        SubjectCategory category,
        [FromBody] SendChannelMessageRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new SendChannelMessageCommand(subjectId, category, request.Content, request.MessageType, request.ReplyToMessageId),
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpPatch("messages/{messageId:guid}/delivered")]
    public async Task<IActionResult> MarkDelivered(Guid messageId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new MarkChannelMessageDeliveredCommand(messageId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPatch("messages/{messageId:guid}/seen")]
    public async Task<IActionResult> MarkSeen(Guid messageId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new MarkChannelMessageSeenCommand(messageId), cancellationToken);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult(Result result)
    {
        if (result.IsSuccess)
            return Ok(result);

        return result.ErrorCode switch
        {
            "UNAUTHORIZED" => Unauthorized(new { result.ErrorCode, result.ErrorMessage }),
            "ACCESS_DENIED" => StatusCode(StatusCodes.Status403Forbidden, new { result.ErrorCode, result.ErrorMessage }),
            "SUBJECT_NOT_FOUND" or "MESSAGE_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
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
            "SUBJECT_NOT_FOUND" or "MESSAGE_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
            _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
        };
    }
}
