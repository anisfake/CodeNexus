using CodeNexus.API.Models.Requests;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.DirectChats.Commands.CreateDirectConversation;
using CodeNexus.Application.Features.DirectChats.Commands.MarkMessageDelivered;
using CodeNexus.Application.Features.DirectChats.Commands.MarkMessageSeen;
using CodeNexus.Application.Features.DirectChats.Commands.SendDirectMessage;
using CodeNexus.Application.Features.DirectChats.Queries.GetDirectChatContacts;
using CodeNexus.Application.Features.DirectChats.Queries.GetConversationMessages;
using CodeNexus.Application.Features.DirectChats.Queries.GetConversations;
using CodeNexus.Application.Features.DirectChats.Queries.GetUnreadCount;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeNexus.API.Controllers;

[ApiController]
[Route("api/direct-chats")]
[Authorize]
public class DirectChatController : ControllerBase
{
    private readonly ISender _sender;

    public DirectChatController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetConversationsQuery(), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("contacts")]
    public async Task<IActionResult> GetContacts(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetDirectChatContactsQuery(), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("conversations/{conversationId:guid}/messages")]
    public async Task<IActionResult> GetConversationMessages(
        Guid conversationId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 30,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new GetConversationMessagesQuery(conversationId, pageNumber, pageSize),
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpPost("conversations")]
    public async Task<IActionResult> CreateConversation(
        [FromBody] CreateDirectConversationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CreateDirectConversationCommand(request.ParticipantId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("conversations/{conversationId:guid}/messages")]
    public async Task<IActionResult> SendMessage(
        Guid conversationId,
        [FromBody] SendDirectMessageRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SendDirectMessageCommand(conversationId, request.Content, request.MessageType);
        var result = await _sender.Send(command, cancellationToken);

        return ToActionResult(result);
    }

    [HttpPatch("messages/{messageId:guid}/delivered")]
    public async Task<IActionResult> MarkDelivered(Guid messageId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new MarkMessageDeliveredCommand(messageId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPatch("messages/{messageId:guid}/seen")]
    public async Task<IActionResult> MarkSeen(Guid messageId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new MarkMessageSeenCommand(messageId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetUnreadCountQuery(), cancellationToken);
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
            "CONVERSATION_NOT_FOUND" or "MESSAGE_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
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
            "CONVERSATION_NOT_FOUND" or "MESSAGE_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
            _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
        };
    }
}
