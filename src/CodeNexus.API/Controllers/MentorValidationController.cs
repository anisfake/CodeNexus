using CodeNexus.API.Models.Requests;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.MentorValidationRequests.Commands.RespondToValidationRequest;
using CodeNexus.Application.Features.MentorValidationRequests.Commands.SubmitValidationRequest;
using CodeNexus.Application.Features.MentorValidationRequests.DTOs;
using CodeNexus.Application.Features.MentorValidationRequests.Queries.GetMyValidationRequests;
using CodeNexus.Application.Features.MentorValidationRequests.Queries.GetValidationRequestInbox;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeNexus.API.Controllers;

[ApiController]
[Route("api/mentor-validation-requests")]
public class MentorValidationController : ControllerBase
{
    private readonly ISender _sender;

    public MentorValidationController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> Submit([FromBody] SubmitValidationRequestRequest request, CancellationToken cancellationToken)
    {
        var command = new SubmitValidationRequestCommand(request.PathId, request.MentorId, request.StudentNote);
        var result = await _sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("my")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetMy([FromQuery] ValidationRequestStatus? status, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetMyValidationRequestsQuery(status), cancellationToken);
        if (result.IsSuccess)
            return Ok(result.Value);
        return BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpGet("inbox")]
    [Authorize(Roles = "Mentor")]
    public async Task<IActionResult> GetInbox([FromQuery] ValidationRequestStatus? status, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetValidationRequestInboxQuery(status), cancellationToken);
        if (result.IsSuccess)
            return Ok(result.Value);
        return BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPut("{validationRequestId:guid}/respond")]
    [Authorize(Roles = "Mentor")]
    public async Task<IActionResult> Respond(Guid validationRequestId, [FromBody] RespondToValidationRequestRequest request, CancellationToken cancellationToken)
    {
        var command = new RespondToValidationRequestCommand(validationRequestId, request.Feedback, request.Accept);
        var result = await _sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult(Result<ValidationRequestDto> result)
    {
        if (result.IsSuccess)
            return Ok(result.Value);

        return result.ErrorCode switch
        {
            "UNAUTHORIZED" => Unauthorized(new { result.ErrorCode, result.ErrorMessage }),
            "LEARNING_PATH_NOT_FOUND" or "MENTOR_NOT_FOUND" or "VALIDATION_REQUEST_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
            "VALIDATION_REQUEST_ALREADY_PENDING" => Conflict(new { result.ErrorCode, result.ErrorMessage }),
            "MENTOR_SUBSCRIPTION_REQUIRED" or "VALIDATION_QUOTA_EXCEEDED" => StatusCode(StatusCodes.Status402PaymentRequired, new { result.ErrorCode, result.ErrorMessage }),
            _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
        };
    }

    private IActionResult ToActionResult(Result result)
    {
        if (result.IsSuccess)
            return Ok(new { Message = "Response submitted successfully." });

        return result.ErrorCode switch
        {
            "UNAUTHORIZED" => Unauthorized(new { result.ErrorCode, result.ErrorMessage }),
            "VALIDATION_REQUEST_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
            "INVALID_STATUS" => BadRequest(new { result.ErrorCode, result.ErrorMessage }),
            _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
        };
    }
}
