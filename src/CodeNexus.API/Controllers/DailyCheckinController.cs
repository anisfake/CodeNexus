using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.DailyCheckin.DTOs;
using CodeNexus.Application.Features.DailyCheckin.Queries.GetDailyCheckinBySessionId;
using CodeNexus.Application.Features.DailyCheckin.Queries.GetMyDailyCheckins;
using CodeNexus.Application.Features.DailyCheckin.Queries.GetMyDailyCheckinStats;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeNexus.API.Controllers;

[Route("api/daily-checkins")]
[ApiController]
[Authorize]
public class DailyCheckinController : ControllerBase
{
    private readonly ISender _sender;

    public DailyCheckinController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("session/{sessionId:guid}")]
    public async Task<IActionResult> GetBySessionId(Guid sessionId, CancellationToken cancellationToken)
    {
        var query = new GetDailyCheckinBySessionIdQuery(sessionId);
        var result = await _sender.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("me/stats")]
    public async Task<IActionResult> GetMyCheckinStats(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetMyDailyCheckinStatsQuery(), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyCheckins([FromQuery] GetMyDailyCheckinsRequest request, CancellationToken cancellationToken)
    {
        var query = new GetMyDailyCheckinsQuery(
            request.FromDate,
            request.ToDate,
            request.PageNumber,
            request.PageSize);

        var result = await _sender.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return result.ErrorCode switch
        {
            "DAILY_CHECKIN_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
            "UNAUTHORIZED" => Unauthorized(new { result.ErrorCode, result.ErrorMessage }),
            _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
        };
    }
}
