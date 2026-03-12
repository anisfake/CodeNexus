using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.FocusSessions.Commands.AbandonSession;
using CodeNexus.Application.Features.FocusSessions.Commands.CompleteSession;
using CodeNexus.Application.Features.FocusSessions.Commands.StartSession;
using CodeNexus.Application.Features.FocusSessions.DTOs;
using CodeNexus.Application.Features.FocusSessions.Queries.GetActiveSession;
using CodeNexus.Application.Features.FocusSessions.Queries.GetSessionHistory;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace CodeNexus.API.Controllers;

[ApiController]
[Authorize]
public class FocusSessionController : ControllerBase
{
    private readonly ISender _sender;

    public FocusSessionController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("api/focus-sessions/start")]
    public async Task<IActionResult> StartSession([FromBody] StartSessionRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequest(new { ErrorCode = "INVALID_REQUEST", ErrorMessage = "Request body is required" });
        }

        var command = new StartSessionCommand(request.TaskId, request.SessionType, request.PlannedDurationMinutes, request.Title);
        var result = await _sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("api/focus-sessions/{sessionId}/complete")]
    public async Task<IActionResult> CompleteSessionWithForm(
        Guid sessionId,
        [FromForm] string? submittedCode = null,
        [FromForm] string? submittedSummary = null,
        [FromForm] bool isEarlyCompletion = false,
        CancellationToken cancellationToken = default)
    {
        var command = new CompleteSessionCommand(sessionId, submittedCode, submittedSummary, isEarlyCompletion);
        var result = await _sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("api/focus-sessions/active/{taskId}")]
    public async Task<IActionResult> GetActiveSession(Guid taskId, CancellationToken cancellationToken)
    {
        var query = new GetActiveSessionQuery(taskId);
        var result = await _sender.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("api/focus-sessions/{sessionId}/abandon")]
    public async Task<IActionResult> AbandonSession(Guid sessionId, CancellationToken cancellationToken)
    {
        var command = new AbandonSessionCommand(sessionId);
        var result = await _sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("api/focus-sessions/history")]
    public async Task<IActionResult> GetSessionHistory(
        [FromQuery] Guid? taskId = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = new GetSessionHistoryQuery(taskId, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult(Result result)
    {
        if (result.IsSuccess)
            return Ok(result);

        return result.ErrorCode switch
        {
            "SESSION_ALREADY_ACTIVE" => Conflict(new { result.ErrorCode, result.ErrorMessage }),
            "SESSION_NOT_RUNNING" => Conflict(new { result.ErrorCode, result.ErrorMessage }),
            "TASK_NOT_FOUND" or "SESSION_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
            "INVALID_DURATION" => BadRequest(new { result.ErrorCode, result.ErrorMessage }),
            _ => StatusCode(StatusCodes.Status500InternalServerError, new { result.ErrorCode, result.ErrorMessage })
        };
    }

    private IActionResult ToActionResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return Ok(result.Value);

        return result.ErrorCode switch
        {
            "SESSION_ALREADY_ACTIVE" => Conflict(new { result.ErrorCode, result.ErrorMessage }),
            "SESSION_NOT_RUNNING" => Conflict(new { result.ErrorCode, result.ErrorMessage }),
            "TASK_NOT_FOUND" or "SESSION_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
            "INVALID_DURATION" => BadRequest(new { result.ErrorCode, result.ErrorMessage }),
            "MISSING_CODE_SUBMISSION" or "MISSING_SUMMARY_SUBMISSION" => BadRequest(new { result.ErrorCode, result.ErrorMessage }),
            _ => StatusCode(StatusCodes.Status500InternalServerError, new { result.ErrorCode, result.ErrorMessage })
        };
    }
}