using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.AIUsageLogs.DTOs;

public record AIUsageLogResponse(
    Guid UsageLogId,
    Guid? UserId,
    Guid? ConfigId,
    AIUsageType UsageType,
    AIAccessTier AccessTierUsed,
    string ProviderName,
    string Model,
    int InputTokens,
    int OutputTokens,
    int TotalTokens,
    decimal CostUsd,
    DateTime CreatedAt
);

public record AIUsageSummaryResponse(
    AIAccessTier AccessTierUsed,
    AIUsageType UsageType,
    string ProviderName,
    string Model,
    int TotalRequests,
    long TotalInputTokens,
    long TotalOutputTokens,
    long TotalTokens,
    decimal TotalCostUsd
);

public record MentorAiQuotaStatusResponse(
    Guid MentorId,
    string Username,
    string Email,
    int UsedPaidRequestsThisMonth,
    int MonthlyLimit,
    decimal UsageRatio,
    bool IsNearLimit,
    bool IsReachedLimit,
    DateTime WindowStartUtc
);
