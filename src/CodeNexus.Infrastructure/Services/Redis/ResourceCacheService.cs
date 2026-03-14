using System.Text.Json;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Resources.DTOs;
using StackExchange.Redis;

namespace CodeNexus.Infrastructure.Services;

public class ResourceCacheService : IResourceCacheService
{
    private readonly IConnectionMultiplexer _redis;
    private const string MyResourcesKeyPrefix = "resource:list:";
    private const string MyResourcesIndexPrefix = "resource:list:index:";
    private const string ResourcePagesKeyPrefix = "resource:pages:";

    public ResourceCacheService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<PaginationDto<ResourceResponse>?> GetMyResourcesAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        Guid? subjectId,
        string? searchTerm,
        int sortBy,
        bool sortDescending,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_redis.IsConnected)
                return null;

            var db = _redis.GetDatabase();
            var key = BuildMyResourcesKey(userId, pageNumber, pageSize, subjectId, searchTerm, sortBy, sortDescending);
            var value = await db.StringGetAsync(key);

            if (!value.HasValue)
                return null;

            return JsonSerializer.Deserialize<PaginationDto<ResourceResponse>>(value!);
        }
        catch
        {
            return null;
        }
    }

    public async Task SetMyResourcesAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        Guid? subjectId,
        string? searchTerm,
        int sortBy,
        bool sortDescending,
        PaginationDto<ResourceResponse> data,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_redis.IsConnected)
                return;

            var db = _redis.GetDatabase();
            var key = BuildMyResourcesKey(userId, pageNumber, pageSize, subjectId, searchTerm, sortBy, sortDescending);
            var payload = JsonSerializer.Serialize(data);
            var indexKey = BuildMyResourcesIndexKey(userId);

            await db.StringSetAsync(key, payload, expiration);
            await db.SetAddAsync(indexKey, key);
            await db.KeyExpireAsync(indexKey, TimeSpan.FromHours(1));
        }
        catch
        {
        }
    }

    public async Task<ResourcePagesResponse?> GetResourcePagesAsync(Guid userId, Guid resourceId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_redis.IsConnected)
                return null;

            var db = _redis.GetDatabase();
            var key = BuildResourcePagesKey(userId, resourceId);
            var value = await db.StringGetAsync(key);

            if (!value.HasValue)
                return null;

            return JsonSerializer.Deserialize<ResourcePagesResponse>(value!);
        }
        catch
        {
            return null;
        }
    }

    public async Task SetResourcePagesAsync(Guid userId, Guid resourceId, ResourcePagesResponse data, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_redis.IsConnected)
                return;

            var db = _redis.GetDatabase();
            var key = BuildResourcePagesKey(userId, resourceId);
            var payload = JsonSerializer.Serialize(data);

            await db.StringSetAsync(key, payload, expiration);
        }
        catch
        {
        }
    }

    public async Task InvalidateUserResourcesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_redis.IsConnected)
                return;

            var db = _redis.GetDatabase();
            var indexKey = BuildMyResourcesIndexKey(userId);
            var members = await db.SetMembersAsync(indexKey);

            if (members.Length > 0)
            {
                await db.KeyDeleteAsync(members.Select(m => (RedisKey)m.ToString()).ToArray());
            }

            await db.KeyDeleteAsync(indexKey);
        }
        catch
        {
        }
    }

    public async Task InvalidateResourcePagesAsync(Guid userId, Guid resourceId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_redis.IsConnected)
                return;

            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(BuildResourcePagesKey(userId, resourceId));
        }
        catch
        {
        }
    }

    private static string BuildMyResourcesKey(Guid userId, int pageNumber, int pageSize, Guid? subjectId, string? searchTerm, int sortBy, bool sortDescending)
    {
        var subject = subjectId.HasValue ? subjectId.Value.ToString("N") : "all";
        var search = string.IsNullOrWhiteSpace(searchTerm) ? "none" : searchTerm.Trim().ToLowerInvariant();
        return $"{MyResourcesKeyPrefix}{userId:N}:{pageNumber}:{pageSize}:{subject}:{search}:{sortBy}:{sortDescending}";
    }

    private static string BuildMyResourcesIndexKey(Guid userId) => $"{MyResourcesIndexPrefix}{userId:N}";

    private static string BuildResourcePagesKey(Guid userId, Guid resourceId) => $"{ResourcePagesKeyPrefix}{userId:N}:{resourceId:N}";
}
