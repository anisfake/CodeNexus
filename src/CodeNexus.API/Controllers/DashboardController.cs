using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Dashboard.Queries.GetStudentDashboardStats;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeNexus.API.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly ISender _sender;

    public DashboardController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("student/stats")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetStudentStats()
    {
        var query = new GetStudentDashboardStatsQuery();
        var result = await _sender.Send(query);

        if (result.IsSuccess)
            return Ok(result.Value);

        return BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }
}
