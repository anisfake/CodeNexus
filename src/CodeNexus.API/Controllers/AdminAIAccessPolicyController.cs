using CodeNexus.API.Models.Requests;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.AIAccessPolicy.Commands.UpdateMentorAiAccessPolicy;
using CodeNexus.Application.Features.AIAccessPolicy.DTOs;
using CodeNexus.Application.Features.AIAccessPolicy.Queries.GetMentorAiAccessPolicy;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeNexus.API.Controllers;

[ApiController]
[Route("api/admin/ai-access-policy")]
[Authorize(Roles = "Admin")]
public class AdminAIAccessPolicyController : ControllerBase
{
    private readonly ISender _sender;

    public AdminAIAccessPolicyController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("mentor")]
    public async Task<IActionResult> GetMentorPolicy(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetMentorAiAccessPolicyQuery(), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut("mentor")]
    public async Task<IActionResult> UpdateMentorPolicy(
        [FromBody] UpdateMentorAiAccessPolicyRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateMentorAiAccessPolicyCommand(
            request.MentorPaidRequestsMonthlyLimit,
            request.MentorDowngradeNotifyCooldownHours);

        var result = await _sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return Ok(result.Value);

        return BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }
}

