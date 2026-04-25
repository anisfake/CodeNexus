using CodeNexus.Application.Features.LearningPathMentorReviews.Commands.SendMentorReviewReminder;
using CodeNexus.Application.Features.LearningPathMentorReviews.Queries.GetAllMentorReviews;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeNexus.API.Controllers;

[ApiController]
[Route("api/admin/mentor-reviews")]
[Authorize(Roles = "Admin")]
public class AdminMentorReviewController : ControllerBase
{
    private readonly ISender _sender;

    public AdminMentorReviewController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] LearningPathMentorReviewDecisionStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new GetAllMentorReviewsQuery(status, page, pageSize), cancellationToken);
        return Ok(result.Value);
    }

    [HttpPost("{reviewId}/send-reminder")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendReminder(Guid reviewId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new SendMentorReviewReminderCommand(reviewId), cancellationToken);

        if (result.IsFailure)
        {
            if (result.ErrorCode == "REVIEW_NOT_FOUND")
                return NotFound(new { result.ErrorCode, result.ErrorMessage });

            return BadRequest(new { result.ErrorCode, result.ErrorMessage });
        }

        return Ok(new { message = "Reminder email sent successfully." });
    }
}
