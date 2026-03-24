using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Domain.Enums;
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

    public async Task<string?> GetApiKeyAsync(AIUsageType usageType, AIAccessTier accessTier, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_redis.IsConnected)
                return null;

            var db = _redis.GetDatabase();
            var key = $"{KeyPrefix}{usageType}:{accessTier}";
            var value = await db.StringGetAsync(key);
            return value.HasValue ? value.ToString() : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task SetApiKeyAsync(AIUsageType usageType, AIAccessTier accessTier, string apiKey, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_redis.IsConnected)
                return;

            var db = _redis.GetDatabase();
            var key = $"{KeyPrefix}{usageType}:{accessTier}";
            await db.StringSetAsync(key, apiKey, expiration);
        }
        catch
        {
            // If Redis fails, silently continue (will read from DB next time)
        }
    }
}
