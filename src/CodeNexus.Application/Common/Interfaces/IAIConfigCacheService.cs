using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Common.Interfaces;

public interface IAIConfigCacheService
{
    Task<string?> GetApiKeyAsync(AIUsageType usageType, CancellationToken cancellationToken = default);
    Task SetApiKeyAsync(AIUsageType usageType, string apiKey, TimeSpan expiration, CancellationToken cancellationToken = default);
}
