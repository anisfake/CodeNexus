using System.Text.Json;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Users.DTOs;
using StackExchange.Redis;

namespace CodeNexus.Infrastructure.Services;

public class UserCacheService : IUserCacheService
{
    private readonly IConnectionMultiplexer _redis;
    private const string MyProfileKeyPrefix = "user:profile:me:";
    private const string UserByIdKeyPrefix = "user:profile:id:";

    public UserCacheService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<UserProfileRespone?> GetMyProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_redis.IsConnected)
                return null;

            var db = _redis.GetDatabase();
            var key = BuildMyProfileKey(userId);
            var value = await db.StringGetAsync(key);

            if (!value.HasValue)
                return null;

            return JsonSerializer.Deserialize<UserProfileRespone>(value!);
        }
        catch
        {
            return null;
        }
    }

    public async Task SetMyProfileAsync(Guid userId, UserProfileRespone profile, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_redis.IsConnected)
                return;

            var db = _redis.GetDatabase();
            var key = BuildMyProfileKey(userId);
            var payload = JsonSerializer.Serialize(profile);

            await db.StringSetAsync(key, payload, expiration);
        }
        catch
        {
        }
    }

    public async Task<UserRespone?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_redis.IsConnected)
                return null;

            var db = _redis.GetDatabase();
            var key = BuildUserByIdKey(userId);
            var value = await db.StringGetAsync(key);

            if (!value.HasValue)
                return null;

            return JsonSerializer.Deserialize<UserRespone>(value!);
        }
        catch
        {
            return null;
        }
    }

    public async Task SetUserByIdAsync(Guid userId, UserRespone user, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_redis.IsConnected)
                return;

            var db = _redis.GetDatabase();
            var key = BuildUserByIdKey(userId);
            var payload = JsonSerializer.Serialize(user);

            await db.StringSetAsync(key, payload, expiration);
        }
        catch
        {
        }
    }

    public async Task InvalidateUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_redis.IsConnected)
                return;

            var db = _redis.GetDatabase();
            var keys = new RedisKey[]
            {
                BuildMyProfileKey(userId),
                BuildUserByIdKey(userId)
            };

            await db.KeyDeleteAsync(keys);
        }
        catch
        {
        }
    }

    private static string BuildMyProfileKey(Guid userId) => $"{MyProfileKeyPrefix}{userId}";

    private static string BuildUserByIdKey(Guid userId) => $"{UserByIdKeyPrefix}{userId}";
}
