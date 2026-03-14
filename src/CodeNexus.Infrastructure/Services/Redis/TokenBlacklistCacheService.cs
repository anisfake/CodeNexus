using CodeNexus.Application.Common.Interfaces;
using StackExchange.Redis;

namespace CodeNexus.Infrastructure.Services;

public class TokenBlacklistCacheService : ITokenBlacklistCacheService
{
    private readonly IConnectionMultiplexer _redis;
    private const string KeyPrefix = "token:blacklist:";

    public TokenBlacklistCacheService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<bool?> GetTokenStatusAsync(string tokenId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_redis.IsConnected || string.IsNullOrWhiteSpace(tokenId))
                return null;

            var db = _redis.GetDatabase();
            var value = await db.StringGetAsync(BuildKey(tokenId));

            if (!value.HasValue)
                return null;

            return bool.TryParse(value.ToString(), out var isBlacklisted)
                ? isBlacklisted
                : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task SetTokenStatusAsync(string tokenId, bool isBlacklisted, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_redis.IsConnected || string.IsNullOrWhiteSpace(tokenId) || expiration <= TimeSpan.Zero)
                return;

            var db = _redis.GetDatabase();
            await db.StringSetAsync(BuildKey(tokenId), isBlacklisted.ToString(), expiration);
        }
        catch
        {
        }
    }

    private static string BuildKey(string tokenId) => $"{KeyPrefix}{tokenId}";
}
