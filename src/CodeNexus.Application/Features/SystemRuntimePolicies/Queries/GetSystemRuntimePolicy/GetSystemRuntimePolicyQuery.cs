using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.SystemRuntimePolicies.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.SystemRuntimePolicies.Queries.GetSystemRuntimePolicy;

public record GetSystemRuntimePolicyQuery(string PolicyKey = "runtime_policy") : IRequest<Result<SystemRuntimePolicyDto>>;
