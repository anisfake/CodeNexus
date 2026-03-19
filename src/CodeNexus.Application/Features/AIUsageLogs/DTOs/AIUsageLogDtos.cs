using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.AIUsageLogs.DTOs;

public record AIUsageLogResponse(
    Guid UsageLogId,
    AIUsageType UsageType,
    string ProviderName,
    string Model,
    int InputTokens,
    int OutputTokens,
    int TotalTokens,
    DateTime CreatedAt
);

public record AIUsageSummaryResponse(
    AIUsageType UsageType,
    int TotalRequests,
    long TotalInputTokens,
    long TotalOutputTokens,
    long TotalTokens
);
