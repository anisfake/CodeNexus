using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Quizzes.Commands.StartQuizAttempt;
using CodeNexus.Application.Features.Quizzes.Commands.SubmitQuizAttempt;
using CodeNexus.Application.Features.Quizzes.DTOs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeNexus.API.Controllers;

[ApiController]
[Route("api/quizzes")]
[Authorize(Roles = "Mentor, Student")]
public class QuizController : ControllerBase
{
    private readonly ISender _sender;

    public QuizController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("{quizId:guid}/start")]
    public async Task<IActionResult> StartQuizAttempt(Guid quizId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new StartQuizAttemptCommand(quizId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("attempts/{attemptId:guid}/submit")]
    public async Task<IActionResult> SubmitQuizAttempt(Guid attemptId, [FromBody] List<AnswerItemDto> answers, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new SubmitQuizAttemptCommand(attemptId, answers), cancellationToken);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return Ok(result.Value);

        return result.ErrorCode switch
        {
            "UNAUTHORIZED" => Unauthorized(new { result.ErrorCode, result.ErrorMessage }),
            "QUIZ_NOT_FOUND" or "ATTEMPT_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
            "ATTEMPT_ALREADY_COMPLETED" or "ATTEMPT_TIME_EXPIRED" => Conflict(new { result.ErrorCode, result.ErrorMessage }),
            _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
        };
    }
}
