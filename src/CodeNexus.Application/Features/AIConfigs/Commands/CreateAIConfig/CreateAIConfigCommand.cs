using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.AIConfigs.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;

namespace CodeNexus.Application.Features.AIConfigs.Commands.CreateAIConfig
{
    public record CreateAIConfigCommand(
        string ProviderName,
        string ApiKey,
        Dictionary<string, object> ConfigJson,
        AIUsageType AIUsageType,
        bool IsEnabled
    ) : IRequest<Result<CreateAIConfigResponse>>;
}
