using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Goals.Commands.CreateGoal;
using CodeNexus.Application.Features.Goals.Commands.DeleteGoal;
using CodeNexus.Application.Features.Goals.Commands.UpdateGoal;
using CodeNexus.Application.Features.Goals.DTOs;
using CodeNexus.Application.Features.Goals.Queries.GetGoals;
using CodeNexus.Application.Features.Goals.Queries.GetGoalMapping;
using CodeNexus.Application.Features.Goals.Queries.GetMyGoal;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeNexus.API.Controllers;

[ApiController]
[Authorize]
public class GoalController : ControllerBase
{
    private readonly ISender _sender;

    public GoalController(ISender sender)
    {
        _sender = sender;
    }


    [HttpGet("api/goals/me")]
    public async Task<IActionResult> GetMyGoals(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetMyGoalQuery(), cancellationToken);

        return ToActionResult(result);
    }

    [HttpGet("api/goals")]
    public async Task<IActionResult> GetGoals(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetGoalsQuery(), cancellationToken);

        return ToActionResult(result);
    }

    [HttpGet("api/goals/{goalId}/mapping")]
    public async Task<IActionResult> GetGoalMapping(Guid goalId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetGoalMappingQuery(goalId), cancellationToken);

        return ToActionResult(result);
    }

    [HttpPost("api/goals")]
    public async Task<IActionResult> CreateGoal(CreateGoalRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateGoalCommand(
            request.SubjectId,
            request.Title,
            request.Description,
            request.Duration);
        var result = await _sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut("api/goals/{goalId}")]
    public async Task<IActionResult> UpdateGoal(Guid goalId, UpdateGoalRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateGoalCommand(
            goalId,
            request.SubjectId,
            request.Title,
            request.Description,
            request.IsActive,
            request.Duration);

        var result = await _sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    [HttpDelete("api/goals/{goalId}")]
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
            "GOAL_ALREADY_EXISTS" or "GOAL_ACTIVE_LIMIT_REACHED" => Conflict(new { result.ErrorCode, result.ErrorMessage }),
            "OTP_RATE_LIMITED" or "RESEND_RATE_LIMITED" => StatusCode(StatusCodes.Status429TooManyRequests, new { result.ErrorCode, result.ErrorMessage }),
            _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
        };
    }
    
    private string GetDurationLabel(GoalDuration duration)
    {
        return duration switch
        {
            GoalDuration.OneWeek => "1 tuần",
            GoalDuration.TwoWeeks => "2 tuần", 
            GoalDuration.OneMonth => "1 tháng",
            GoalDuration.TwoMonths => "2 tháng",
            GoalDuration.ThreeMonths => "3 tháng",
            GoalDuration.SixMonths => "6 tháng",
            _ => duration.ToString()
        };
    }
}
