using CodeNexus.Application.Common.Interfaces;
using StackExchange.Redis;

namespace CodeNexus.Infrastructure.Services;

public class AIConfigCacheService : IAIConfigCacheService
{
    private readonly IConnectionMultiplexer _redis;
    private const string KeyPrefix = "aiconfig:apikey:";

    public AIConfigCacheService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<string?> GetApiKeyAsync(string providerName, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_redis.IsConnected)
                return null;

            var db = _redis.GetDatabase();
            var key = $"{KeyPrefix}{providerName.ToLower()}";
            var value = await db.StringGetAsync(key);
            return value.HasValue ? value.ToString() : null;
        }
        catch
        {
            // If Redis fails, return null to force reading from DB
            return null;
        }
    }

    public async Task SetApiKeyAsync(string providerName, string apiKey, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_redis.IsConnected)
                return;

            var db = _redis.GetDatabase();
            var key = $"{KeyPrefix}{providerName.ToLower()}";
            await db.StringSetAsync(key, apiKey, expiration);
        }
        catch
        {
            // If Redis fails, silently continue (will read from DB next time)
        }
    }
}
