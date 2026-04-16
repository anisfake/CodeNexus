using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.SystemRuntimePolicies.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.SystemRuntimePolicies.Queries.GetAllSystemRuntimePolicies;

public record GetAllSystemRuntimePoliciesQuery : IRequest<Result<List<SystemRuntimePolicyDto>>>;
