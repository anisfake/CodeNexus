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
        string ProviderName,
        AIUsageType UsageType,
        AIAccessTier AccessTier,
        bool IsActive,
        DateTime LastUpdated,
        Dictionary<string, object> ConfigJson,
        Dictionary<string, object> ChatPolicy
    );

    public record TestAIProviderRequest(
        string ProviderName,
        string ApiKey,
        Dictionary<string, object>? ConfigJson = null,
        bool JsonMode = false
    );

    public record TestAIProviderResponse(
        bool IsValid,
        string ProviderName,
        string Model,
        string ResponsePreview,
        int InputTokens,
        int OutputTokens,
        int TotalTokens
    );

    public record TestStoredAIProviderRequest(
        bool OnlyActive = true,
        string? ProviderName = null
    );

    public record TestStoredAIProviderResponse(
        Guid ConfigId,
        string ProviderName,
        AIUsageType UsageType,
        AIAccessTier AccessTier,
        bool IsActive,
        bool IsValid,
        string Model,
        string ResponsePreview,
        int InputTokens,
        int OutputTokens,
        int TotalTokens,
        string? ErrorCode,
        string? ErrorMessage
    );

    public record StoredAIProviderKeyResponse(
        Guid ConfigId,
        string ProviderName,
        AIUsageType UsageType,
        AIAccessTier AccessTier,
        bool IsActive,
        DateTime LastUpdated,
        string ApiKey,
        string MaskedApiKey,
        string Model,
        string? ReadError
    );
}
