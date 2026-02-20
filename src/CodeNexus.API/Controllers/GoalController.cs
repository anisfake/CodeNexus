using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Goals.Commands.CreateGoal;
using CodeNexus.Application.Features.Goals.Commands.DeleteGoal;
using CodeNexus.Application.Features.Goals.Commands.UpdateGoal;
using CodeNexus.Application.Features.Goals.DTOs;
using CodeNexus.Application.Features.Goals.Queries.GetGoals;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeNexus.API.Controllers;

[ApiController]
[Route("api/goals")]
[Authorize]
public class GoalController : ControllerBase
{
    private readonly ISender _sender;

    public GoalController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetGoals(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetGoalsQuery(), cancellationToken);

        return ToActionResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateGoal(CreateGoalRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateGoalCommand(request.Title, request.Description, request.DurationDays);
        var result = await _sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut("{goalId}")]
    public async Task<IActionResult> UpdateGoal(Guid goalId, UpdateGoalRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateGoalCommand(goalId, request.Title, request.Description, request.CompleteAt, request.DurationDays, request.IsCompleted);

        var result = await _sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    [HttpDelete("{goalId}")]
    public async Task<IActionResult> DeleteGoal(Guid goalId, CancellationToken cancellationToken)
    {
        var command = new DeleteGoalCommand(goalId);

        var result = await _sender.Send(command, cancellationToken);

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
