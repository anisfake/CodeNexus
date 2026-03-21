using CodeNexus.API.Models.Requests;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Subscriptions.Commands.CreateSubscriptionPlan;
using CodeNexus.Application.Features.Subscriptions.Commands.DeleteSubscriptionPlan;
using CodeNexus.Application.Features.Subscriptions.Commands.UpdateSubscriptionPlan;
using CodeNexus.Application.Features.Subscriptions.DTOs;
using CodeNexus.Application.Features.Subscriptions.Queries.GetSubscriptionPlans;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeNexus.API.Controllers;

[ApiController]
[Route("api/admin/subscription-plans")]
[Authorize(Roles = "Admin")]
public class AdminSubscriptionPlanController : ControllerBase
{
    private readonly ISender _sender;

    public AdminSubscriptionPlanController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetSubscriptionPlansQuery(false), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSubscriptionPlanRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CreateSubscriptionPlanCommand(
            request.PlanType,
            request.Name,
            request.Description,
            request.PriceVnd,
            request.DurationDays,
            request.IsActive,
            request.DisplayOrder), cancellationToken);

        return ToActionResult(result);
    }

    [HttpPut("{subscriptionPlanId:guid}")]
    public async Task<IActionResult> Update(Guid subscriptionPlanId, [FromBody] UpdateSubscriptionPlanRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new UpdateSubscriptionPlanCommand(
            subscriptionPlanId,
            request.PlanType,
            request.Name,
            request.Description,
            request.PriceVnd,
            request.DurationDays,
            request.IsActive,
            request.DisplayOrder), cancellationToken);

        return ToActionResult(result);
    }

    [HttpDelete("{subscriptionPlanId:guid}")]
    public async Task<IActionResult> Delete(Guid subscriptionPlanId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeleteSubscriptionPlanCommand(subscriptionPlanId), cancellationToken);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return Ok(result.Value);

        return result.ErrorCode switch
        {
            "SUBSCRIPTION_PLAN_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
            "SUBSCRIPTION_PLAN_EXISTS" => Conflict(new { result.ErrorCode, result.ErrorMessage }),
            _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
        };
    }
}
