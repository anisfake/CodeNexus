using System.Text.Json;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Subjects.DTOs;
using CodeNexus.Domain.Enums;
using StackExchange.Redis;

namespace CodeNexus.Infrastructure.Services;

public class SubjectCacheService : ISubjectCacheService
{
    private readonly IConnectionMultiplexer _redis;
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(10);
    private const string KeyPrefix = "subject:list:";

    public SubjectCacheService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<List<SubjectDto>?> GetSubjectsAsync(SubjectCategory? category, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_redis.IsConnected)
                return null;

            var db = _redis.GetDatabase();
            var key = BuildKey(category);
            var value = await db.StringGetAsync(key);

            if (!value.HasValue)
                return null;

            return JsonSerializer.Deserialize<List<SubjectDto>>(value!);
        }
        catch
        {
            return null;
        }
    }

    public async Task SetSubjectsAsync(SubjectCategory? category, List<SubjectDto> subjects, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_redis.IsConnected)
                return;

            var db = _redis.GetDatabase();
            var key = BuildKey(category);
            var payload = JsonSerializer.Serialize(subjects);
            var cacheExpiration = expiration <= TimeSpan.Zero ? DefaultExpiration : expiration;

            await db.StringSetAsync(key, payload, cacheExpiration);
        }
        catch
        {
        }
    }

    public async Task InvalidateSubjectsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_redis.IsConnected)
                return;

            var db = _redis.GetDatabase();
            var keys = new List<RedisKey> { BuildKey(null) };

            foreach (var category in Enum.GetValues<SubjectCategory>())
            {
                keys.Add(BuildKey(category));
            }

            await db.KeyDeleteAsync(keys.ToArray());
        }
        catch
        {
        }
    }

    private static string BuildKey(SubjectCategory? category)
    {
        return category.HasValue
            ? $"{KeyPrefix}{category.Value}"
            : $"{KeyPrefix}all";
    }
}
