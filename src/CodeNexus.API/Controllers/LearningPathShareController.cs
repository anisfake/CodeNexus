using CodeNexus.API.Models.Requests;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPathShares.Commands.ApplyLearningPathShareUpdate;
using CodeNexus.Application.Features.LearningPathShares.Commands.AcceptLearningPathShare;
using CodeNexus.Application.Features.LearningPathShares.Commands.RejectLearningPathShare;
using CodeNexus.Application.Features.LearningPathShares.Commands.SendLearningPathShare;
using CodeNexus.Application.Features.LearningPathShares.Queries.GetLearningPathShareUpdateContext;
using CodeNexus.Application.Features.LearningPathShares.Queries.GetPendingLearningPathShares;
using CodeNexus.Application.Features.LearningPathShares.Queries.GetLearningPathSharePreview;
using CodeNexus.Application.Features.LearningPathShares.Queries.GetSentLearningPathShares;
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

    [HttpPost("{shareId:guid}/reject")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> RejectShare(Guid shareId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new RejectLearningPathShareCommand(shareId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("pending")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetPendingShares(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetPendingLearningPathSharesQuery(), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{shareId:guid}/preview")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> PreviewShare(Guid shareId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetLearningPathSharePreviewQuery(shareId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{shareId:guid}/update-context")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetShareUpdateContext(Guid shareId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetLearningPathShareUpdateContextQuery(shareId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{shareId:guid}/apply-update")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> ApplyShareUpdate(
        Guid shareId,
        [FromBody] ApplyLearningPathShareUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ApplyLearningPathShareUpdateCommand(shareId, request.Action), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("sent")]
    [Authorize(Roles = "Mentor, Student")]
    public async Task<IActionResult> GetSentShares([FromQuery] GetSentLearningPathSharesRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetSentLearningPathSharesQuery(request.Status, request.StudentId), cancellationToken);
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
            "USER_NOT_FOUND" or "LEARNING_PATH_NOT_FOUND" or "SOURCE_LEARNING_PATH_NOT_FOUND" or "STUDENT_NOT_FOUND" or "SHARE_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
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
            "USER_NOT_FOUND" or "LEARNING_PATH_NOT_FOUND" or "SOURCE_LEARNING_PATH_NOT_FOUND" or "STUDENT_NOT_FOUND" or "SHARE_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
            "SHARE_ALREADY_PENDING" => Conflict(new { result.ErrorCode, result.ErrorMessage }),
            _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
        };
    }
}
