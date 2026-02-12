using CodeNexus.Application.Features.Goals.Queries.GetGoals;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeNexus.API.Controllers;

[ApiController]
[Route("api/[controller]")]
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

        if (result.IsSuccess)
            return Ok(result.Value);

        return BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }
}
