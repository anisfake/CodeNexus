using CodeNexus.Application.Features.AIConfigs.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.AIConfigs.Queries.GetAICapability;

public record GetAICapabilityQuery() : IRequest<GetAICapabilityResponse>;
