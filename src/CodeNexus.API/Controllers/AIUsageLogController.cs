using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.AIUsageLogs.DTOs;
using CodeNexus.Application.Features.AIUsageLogs.Queries.GetAIUsageLogs;
using CodeNexus.Application.Features.AIUsageLogs.Queries.GetAIProfitOverview;
using CodeNexus.Application.Features.AIUsageLogs.Queries.GetAIUsageSummary;
using CodeNexus.Application.Features.AIUsageLogs.Queries.GetMentorAiQuotaStatus;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeNexus.API.Controllers;

[ApiController]
[Route("api/admin/ai-usage-logs")]
[Authorize(Roles = "Admin")]
public class AIUsageLogController : ControllerBase
{
    private readonly ISender _sender;

    public AIUsageLogController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PaginationDto<AIUsageLogResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUsageLogs(
        [FromQuery] GetAIUsageLogsQuery query,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("summary")]
    [ProducesResponseType(typeof(List<AIUsageSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUsageSummary(
        [FromQuery] GetAIUsageSummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("profit-overview")]
    [ProducesResponseType(typeof(AIProfitOverviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetProfitOverview(
        [FromQuery] GetAIProfitOverviewQuery query,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("mentor-quota-status")]
    [ProducesResponseType(typeof(PaginationDto<MentorAiQuotaStatusResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMentorQuotaStatus(
        [FromQuery] GetMentorAiQuotaStatusQuery query,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return Ok(result.Value);

        return result.ErrorCode switch
        {
            _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
        };
    }
}
