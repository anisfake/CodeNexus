namespace CodeNexus.Application.Common.Interfaces;

public interface IAIConfigCacheService
{
    Task<string?> GetApiKeyAsync(string providerName, CancellationToken cancellationToken = default);
    Task SetApiKeyAsync(string providerName, string apiKey, TimeSpan expiration, CancellationToken cancellationToken = default);
}
