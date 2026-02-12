using Azure.Core;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Chapters.Commands.GenerateChapterContent;
using CodeNexus.Application.Features.LearningPathSkeleton.Commands.GenerateLearningPathSkeleton;
using CodeNexus.Application.Features.Lessons.Commands.GenerateLessonContent;
using CodeNexus.Application.Features.Quizzes.Commands.GenerateQuizQuestions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading;

namespace CodeNexus.API.Controllers;

[ApiController]
[Route("api/learningpaths")]
[Authorize]
public class LearningPathController : ControllerBase
{
    private readonly ISender _sender;
    public LearningPathController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("")]
    public async Task<IActionResult> GenerateSkeleton([FromBody] GenerateLearningPathSkeletonCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { errorCode = result.ErrorCode, errorMessage = result.ErrorMessage });
        }

        return ToActionResult(result);
    }

    [HttpPost("lessons/{lessonId:guid}/content")]
    public async Task<IActionResult> GenerateLessonContent(Guid lessonId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GenerateLessonContentCommand(lessonId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("chapters/{chapterId:guid}/generate-content")]
    public async Task<IActionResult> GenerateChapterContent(Guid chapterId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GenerateChapterContentCommand(chapterId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("quizzes/{quizId:guid}/generate-questions")]
    public async Task<IActionResult> GenerateQuizQuestions(Guid quizId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GenerateQuizQuestionsCommand(quizId), cancellationToken);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult(Result result)
    {
        if (result.IsSuccess)
            return Ok(result);

        return result.ErrorCode switch
        {
            "EMAIL_EXISTS" or "USERNAME_EXISTS" => Conflict(new { result.ErrorCode, result.ErrorMessage }),
            "OTP_RATE_LIMITED" or "RESEND_RATE_LIMITED" => StatusCode(StatusCodes.Status429TooManyRequests, new { result.ErrorCode, result.ErrorMessage }),
            _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
        };
    }

    private IActionResult ToActionResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return Ok(result.Value);

        return result.ErrorCode switch
        {
            "UNAUTHORIZED" or "USERNAME_EXISTS" => Unauthorized(new { result.ErrorCode, result.ErrorMessage }),
            "OTP_RATE_LIMITED" or "RESEND_RATE_LIMITED" => StatusCode(StatusCodes.Status429TooManyRequests, new { result.ErrorCode, result.ErrorMessage }),
            _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
        };
    }
}

