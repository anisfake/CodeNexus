using CodeNexus.Application.Features.Timeline.Queries.GetStudentTimeline;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeNexus.API.Controllers;

[ApiController]
[Route("api/timeline")]
[Authorize]
public class TimelineController : ControllerBase
{
    private readonly ISender _sender;

    public TimelineController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetTimeline(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] Guid? learningPathId,
        [FromQuery] bool onlyActivePaths = true,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new GetStudentTimelineQuery(fromUtc, toUtc, learningPathId, onlyActivePaths),
            cancellationToken);

        if (result.IsSuccess)
            return Ok(result.Value);

        return result.ErrorCode switch
        {
            "LEARNING_PATH_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
            _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
        };
    }

    [HttpGet("today")]
    public async Task<IActionResult> GetTodayTimeline(
        [FromQuery] Guid? learningPathId,
        [FromQuery] bool onlyActivePaths = true,
        CancellationToken cancellationToken = default)
    {
        var (fromUtc, toUtc) = GetVietnamTodayWindowUtc();
        var result = await _sender.Send(
            new GetStudentTimelineQuery(fromUtc, toUtc, learningPathId, onlyActivePaths),
            cancellationToken);

        if (result.IsSuccess)
            return Ok(result.Value);

        return result.ErrorCode switch
        {
            "LEARNING_PATH_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
            _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
        };
    }

    private static (DateTime fromUtc, DateTime toUtc) GetVietnamTodayWindowUtc()
    {
        var timezone = ResolveVietnamTimeZone();
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timezone);
        var startLocal = new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var endLocal = startLocal.AddDays(1).AddTicks(-1);

        return (
            TimeZoneInfo.ConvertTimeToUtc(startLocal, timezone),
            TimeZoneInfo.ConvertTimeToUtc(endLocal, timezone));
    }

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
    }
}

