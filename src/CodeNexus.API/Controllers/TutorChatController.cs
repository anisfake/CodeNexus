using CodeNexus.API.Models.Requests;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.TutorChat.Commands.SendTutorMessage;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeNexus.API.Controllers;

[ApiController]
[Route("api/tutor-chat")]
[Authorize]
public class TutorChatController : ControllerBase
{
    private readonly ISender _sender;

    public TutorChatController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("messages")]
    public async Task<IActionResult> SendTutorMessage([FromBody] SendTutorMessageRequest request, CancellationToken cancellationToken)
    {
        var command = new SendTutorMessageCommand(
            request.ConversationId,
            request.LearningPathId,
            request.ChapterId,
            request.LessonId,
            request.Message);

        var result = await _sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult(Result result)
    {
        if (result.IsSuccess)
            return Ok(result);

        return result.ErrorCode switch
        {
            "UNAUTHORIZED" => Unauthorized(new { result.ErrorCode, result.ErrorMessage }),
            "CONVERSATION_NOT_FOUND" or "LEARNING_PATH_NOT_FOUND" or "CHAPTER_NOT_FOUND" or "LESSON_NOT_FOUND"
                => NotFound(new { result.ErrorCode, result.ErrorMessage }),
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
            "CONVERSATION_NOT_FOUND" or "LEARNING_PATH_NOT_FOUND" or "CHAPTER_NOT_FOUND" or "LESSON_NOT_FOUND"
                => NotFound(new { result.ErrorCode, result.ErrorMessage }),
            _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
        };
    }
}
