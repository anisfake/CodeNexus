using MediatR;
using CodeNexus.Application.Features.AIConfigs.DTOs;
using CodeNexus.Application.Common.Models;

namespace CodeNexus.Application.Features.AIConfigs.Commands.UpdateAIConfig;

public record UpdateAIConfigCommand(
    string ProviderName,
    string? ApiKey,
    Dictionary<string, object>? ConfigJson,
    bool? IsEnabled
) : IRequest<Result<UpdateAIConfigResponse>>;
