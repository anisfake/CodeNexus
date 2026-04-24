using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.TaskReviews.Commands.RequestTaskReview;
using CodeNexus.Application.Features.TaskReviews.Commands.SubmitTaskReview;
using CodeNexus.Application.Features.TaskReviews.DTOs;
using CodeNexus.Application.Features.TaskReviews.Queries.GetTaskReview;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeNexus.API.Controllers;

[ApiController]
[Authorize]
public class TaskReviewController : ControllerBase
{
    private readonly ISender _sender;

    public TaskReviewController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("api/task-reviews/sessions/{sessionId}/request")]
    public async Task<IActionResult> RequestTaskReview(
        Guid sessionId,
        [FromBody] RequestTaskReviewBody body,
        CancellationToken cancellationToken)
    {
        var command = new RequestTaskReviewCommand(sessionId, body.MentorId, body.StudentRequestNote);
        var result = await _sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut("api/task-reviews/{reviewId}")]
    public async Task<IActionResult> SubmitTaskReview(
        Guid reviewId,
        [FromBody] SubmitTaskReviewBody body,
        CancellationToken cancellationToken)
    {
        var command = new SubmitTaskReviewCommand(reviewId, body.Score, body.Feedback, body.Suggestions);
        var result = await _sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("api/task-reviews/{reviewId}")]
    public async Task<IActionResult> GetTaskReview(Guid reviewId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetTaskReviewQuery(reviewId), cancellationToken);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult(Result result)
    {
        if (result.IsSuccess)
        {
            return Ok(new { message = "Success" });
        }

        return result.ErrorCode switch
        {
            "UNAUTHORIZED" => Unauthorized(new { result.ErrorCode, result.ErrorMessage }),
            "REVIEW_NOT_FOUND" or "SESSION_NOT_FOUND" or "SUBSCRIPTION_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
            "TASK_REVIEW_LIMIT_REACHED" => StatusCode(StatusCodes.Status429TooManyRequests, new { result.ErrorCode, result.ErrorMessage }),
            _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
        };
    }

    private IActionResult ToActionResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return result.ErrorCode switch
        {
            "UNAUTHORIZED" => Unauthorized(new { result.ErrorCode, result.ErrorMessage }),
            "REVIEW_NOT_FOUND" or "SESSION_NOT_FOUND" or "SUBSCRIPTION_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
            "TASK_REVIEW_LIMIT_REACHED" => StatusCode(StatusCodes.Status429TooManyRequests, new { result.ErrorCode, result.ErrorMessage }),
            _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
        };
    }
}

public record RequestTaskReviewBody(Guid MentorId, string? StudentRequestNote);
public record SubmitTaskReviewBody(int Score, string Feedback, string? Suggestions);
