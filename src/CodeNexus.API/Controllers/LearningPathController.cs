using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.Commands.GenerateLearningPathSkeleton;
using CodeNexus.Application.Features.Lessons.Commands.GenerateLessonContent;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeNexus.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LearningPathController : ControllerBase
{
    private readonly IMediator _mediator;

    public LearningPathController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("generate-skeleton")]
    public async Task<IActionResult> GenerateSkeleton([FromBody] GenerateLearningPathSkeletonCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { errorCode = result.ErrorCode, errorMessage = result.ErrorMessage });
        }

        return ToActionResult(result);
    }

    [HttpPost("lessons/{lessonId:guid}/generate-content")]
    public async Task<IActionResult> GenerateLessonContent(Guid lessonId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GenerateLessonContentCommand(lessonId), cancellationToken);
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
