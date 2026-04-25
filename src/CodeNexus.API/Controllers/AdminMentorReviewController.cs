using CodeNexus.Application.Features.LearningPathMentorReviews.Commands.SendMentorReviewReminder;
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

    /// <summary>
    /// Gửi mail nhắc nhở student phản hồi review của mentor (dùng cho demo).
    /// </summary>
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
