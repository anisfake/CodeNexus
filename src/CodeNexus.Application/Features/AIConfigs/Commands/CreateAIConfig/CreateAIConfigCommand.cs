using MediatR;
using CodeNexus.Application.Features.AIConfigs.DTOs;
using CodeNexus.Application.Common.Models;

namespace CodeNexus.Application.Features.AIConfigs.Commands.CreateAIConfig
{
    public record CreateAIConfigCommand(
        string ProviderName,
        string ApiKey,
        Dictionary<string, object> ConfigJson,
        bool IsEnabled
    ) : IRequest<Result<CreateAIConfigResponse>>;
}
