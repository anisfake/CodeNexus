using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.SystemRuntimePolicies.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.SystemRuntimePolicies.Commands.CreateSystemRuntimePolicy;

public record CreateSystemRuntimePolicyCommand(
    string PolicyKey,
    string? Description,
    Dictionary<string, object> ConfigJson,
    bool IsActive = true
) : IRequest<Result<SystemRuntimePolicyDto>>;

