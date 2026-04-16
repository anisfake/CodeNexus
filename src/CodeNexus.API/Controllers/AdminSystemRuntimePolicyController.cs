using CodeNexus.API.Models.Requests;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.SystemRuntimePolicies.Commands.CreateSystemRuntimePolicy;
using CodeNexus.Application.Features.SystemRuntimePolicies.Commands.UpdateSystemRuntimePolicy;
using CodeNexus.Application.Features.SystemRuntimePolicies.DTOs;
using CodeNexus.Application.Features.SystemRuntimePolicies.Queries.GetAllSystemRuntimePolicies;
using CodeNexus.Application.Features.SystemRuntimePolicies.Queries.GetSystemRuntimePolicy;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeNexus.API.Controllers;

[ApiController]
[Route("api/admin/system-runtime-policy")]
[Authorize(Roles = "Admin")]
public class AdminSystemRuntimePolicyController : ControllerBase
{
    private readonly ISender _sender;
    private const string DefaultPolicyKey = "runtime_policy";

    public AdminSystemRuntimePolicyController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllPolicies(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetAllSystemRuntimePoliciesQuery(), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreatePolicy(
        [FromBody] CreateSystemRuntimePolicyRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateSystemRuntimePolicyCommand(
            request.PolicyKey,
            request.Description,
            request.ConfigJson ?? new Dictionary<string, object>(),
            request.IsActive);

        var result = await _sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{policyKey?}")]
    public async Task<IActionResult> GetPolicy(string? policyKey, CancellationToken cancellationToken)
    {
        var normalizedPolicyKey = string.IsNullOrWhiteSpace(policyKey)
            ? DefaultPolicyKey
            : policyKey.Trim();

        var result = await _sender.Send(new GetSystemRuntimePolicyQuery(normalizedPolicyKey), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut("{policyKey?}")]
    public async Task<IActionResult> UpdatePolicy(
        string? policyKey,
        [FromBody] UpdateSystemRuntimePolicyRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedPolicyKey = string.IsNullOrWhiteSpace(policyKey)
            ? DefaultPolicyKey
            : policyKey.Trim();

        var command = new UpdateSystemRuntimePolicyCommand(
            normalizedPolicyKey,
            request.Description,
            request.ConfigJson ?? new Dictionary<string, object>(),
            request.IsActive);

        var result = await _sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return Ok(result.Value);

        return result.ErrorCode switch
        {
            "POLICY_ALREADY_EXISTS" => Conflict(new { result.ErrorCode, result.ErrorMessage }),
            _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
        };
    }
}
