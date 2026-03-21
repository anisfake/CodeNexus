using CodeNexus.Domain.Enums;
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
        bool IsEnabel,
        AIUsageType AIUsageType,
        AIAccessTier AccessTier
    );
    public record CreateAIConfigResponse(
        string Message,
        string ProviderName,
        bool IsEnabled
    );

    public record UpdateAIConfigRequest(
        string? ApiKey,
        string? ProviderName,
        Dictionary<string, object>? ConfigJson,
        bool? IsActive,
        AIUsageType? UsageType,
        AIAccessTier? AccessTier
    );

    public record UpdateAIConfigResponse(
        string Message,
        string ProviderName,
        bool IsEnabled
    );

    public record SetActiveConfigRequest(
        AIUsageType UsageType,
        AIAccessTier AccessTier
    );

    public record AIConfigResponse(
        string ProviderName,
        bool IsEnabled,
        DateTime LastUpdated,
        Dictionary<string, object> Schema
    );
    public record GetAllAIConfigResponse(
        Guid ConfigId,
        string ApiKey,
        string ProviderName,
        AIUsageType UsageType,
        AIAccessTier AccessTier,
        bool IsActive,
        DateTime LastUpdated,
        Dictionary<string, object> ConfigJson
    );
}
