using CodeNexus.API.Models.Requests;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPathShares.Commands.AcceptLearningPathShare;
using CodeNexus.Application.Features.LearningPathShares.Commands.SendLearningPathShare;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeNexus.API.Controllers;

[ApiController]
[Route("api/learningpath-shares")]
[Authorize]
public class LearningPathShareController : ControllerBase
{
    private readonly ISender _sender;

    public LearningPathShareController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("paths/{pathId:guid}")]
    [Authorize(Roles = "Mentor")]
    public async Task<IActionResult> SendShare(
        Guid pathId,
        [FromBody] SendLearningPathShareRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new SendLearningPathShareCommand(pathId, request.StudentId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{shareId:guid}/accept")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> AcceptShare(Guid shareId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new AcceptLearningPathShareCommand(shareId), cancellationToken);
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
            "LEARNING_PATH_NOT_FOUND" or "STUDENT_NOT_FOUND" or "SHARE_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
            "SHARE_ALREADY_PENDING" => Conflict(new { result.ErrorCode, result.ErrorMessage }),
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
            "LEARNING_PATH_NOT_FOUND" or "STUDENT_NOT_FOUND" or "SHARE_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
            "SHARE_ALREADY_PENDING" => Conflict(new { result.ErrorCode, result.ErrorMessage }),
            _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
        };
    }
}
