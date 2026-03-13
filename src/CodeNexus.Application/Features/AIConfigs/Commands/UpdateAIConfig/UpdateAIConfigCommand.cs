using MediatR;
using CodeNexus.Application.Features.AIConfigs.DTOs;
using CodeNexus.Application.Common.Models;
using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.AIConfigs.Commands.UpdateAIConfig;

public record UpdateAIConfigCommand(
    Guid ConfigId,
    string? ProviderName,
    string? ApiKey,
    Dictionary<string, object>? ConfigJson,
    bool? IsActive,
    AIUsageType? UsageType
) : IRequest<Result<UpdateAIConfigResponse>>;
