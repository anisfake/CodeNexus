using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Common.Interfaces;

public interface IAIConfigCacheService
{
    Task<string?> GetApiKeyAsync(AIUsageType usageType, AIAccessTier accessTier, CancellationToken cancellationToken = default);
    Task SetApiKeyAsync(AIUsageType usageType, AIAccessTier accessTier, string apiKey, TimeSpan expiration, CancellationToken cancellationToken = default);
}
