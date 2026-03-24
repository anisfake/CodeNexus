using CodeNexus.Application.Features.Subscriptions.Queries.GetCurrentSubscription;
using CodeNexus.Application.Features.Subscriptions.Queries.GetSubscriptionPlans;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeNexus.API.Controllers;

[ApiController]
[Route("api/subscription-plans")]
public class SubscriptionPlanController : ControllerBase
{
    private readonly ISender _sender;

    public SubscriptionPlanController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetPlans(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetSubscriptionPlansQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentPlan(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetCurrentSubscriptionQuery(), cancellationToken);
        return Ok(result);
    }
}
