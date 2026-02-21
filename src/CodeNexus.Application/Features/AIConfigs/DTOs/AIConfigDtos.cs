using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Application.Features.AIConfigs.DTOs
{
    public record CreateAIConfigRequest(
        string ProviderName,
        string ApiKey,
        Dictionary<string, object> ConfigJson,
        bool IsEnabel
    );
    public record CreateAIConfigResponse(
        string Message,
        string ProviderName,
        bool IsEnabled
    );

    public record UpdateAIConfigRequest(
        string? ApiKey,
        Dictionary<string, object>? ConfigJson,
        bool? IsEnabled
    );

    public record UpdateAIConfigResponse(
        string Message,
        string ProviderName,
        bool IsEnabled
    );

    public record AIConfigResponse(
        string ProviderName,
        bool IsEnabled,
        DateTime LastUpdated,
        Dictionary<string, object> Schema
    );
    public record GetAllAIConfigResponse(
        string ApiKey,
        string ProviderName,
        bool IsEnabled,
        DateTime LastUpdated,
        Dictionary<string, object> ConfigJson
    );
}
