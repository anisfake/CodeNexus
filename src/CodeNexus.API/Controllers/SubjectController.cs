using CodeNexus.Application.Features.Subjects.Queries.GetSubjects;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeNexus.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SubjectController : ControllerBase
{
    private readonly ISender _sender;

    public SubjectController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetSubjects(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetSubjectsQuery(), cancellationToken);

        if (result.IsSuccess)
            return Ok(result.Value);

        return BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }
}
