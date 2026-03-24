using CodeNexus.Application.Features.AIConfigs.Queries.GetAICapability;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeNexus.API.Controllers;

[ApiController]
[Route("api/ai/capability")]
[Authorize]
public class AICapabilityController : ControllerBase
{
    private readonly ISender _sender;

    public AICapabilityController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetAICapabilityQuery(), cancellationToken);
        return Ok(result);
    }
}
