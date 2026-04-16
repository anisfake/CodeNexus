using CodeNexus.API.Models.Requests;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.DailyCheckin.DTOs;
using CodeNexus.Application.Features.DailyCheckin.Queries.GetMyDailyCheckins;
using CodeNexus.Application.Features.DailyCheckin.Queries.GetMyDailyCheckinStatus;
using CodeNexus.Application.Features.DailyCheckin.Queries.GetMyDailyCheckinStats;
using CodeNexus.Application.Features.DailyCheckin.Queries.GetMyTodayDailyCheckin;
using CodeNexus.Application.Features.DailyCheckin.Commands.SetDailyMood;
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

    [HttpGet("me/today")]
    public async Task<IActionResult> GetMyTodayCheckin(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetMyTodayDailyCheckinQuery(), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("me/stats")]
    public async Task<IActionResult> GetMyCheckinStats(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetMyDailyCheckinStatsQuery(), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("me/status")]
    public async Task<IActionResult> GetMyCheckinStatus(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetMyDailyCheckinStatusQuery(), cancellationToken);
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

    [HttpPost("me/mood")]
    public async Task<IActionResult> SetMyMood([FromBody] SetDailyMoodRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new SetDailyMoodCommand(request.Mood), cancellationToken);
        if (result.IsSuccess) return Ok();
        return result.ErrorCode switch
        {
            "UNAUTHORIZED" => Unauthorized(new { result.ErrorCode, result.ErrorMessage }),
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
            "DAILY_CHECKIN_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
            "UNAUTHORIZED" => Unauthorized(new { result.ErrorCode, result.ErrorMessage }),
            _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
        };
    }
}
