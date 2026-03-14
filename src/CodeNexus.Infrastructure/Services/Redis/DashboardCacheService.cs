using System.Text.Json;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Dashboard.DTOs;
using StackExchange.Redis;

namespace CodeNexus.Infrastructure.Services;

public class DashboardCacheService : IDashboardCacheService
{
    private readonly IConnectionMultiplexer _redis;
    private const string StudentStatsKeyPrefix = "dashboard:student:stats:";

    public DashboardCacheService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<StudentDashboardStatsResponse?> GetStudentStatsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_redis.IsConnected)
                return null;

            var db = _redis.GetDatabase();
            var value = await db.StringGetAsync(BuildStudentStatsKey(userId));

            if (!value.HasValue)
                return null;

            return JsonSerializer.Deserialize<StudentDashboardStatsResponse>(value!);
        }
        catch
        {
            return null;
        }
    }

    public async Task SetStudentStatsAsync(Guid userId, StudentDashboardStatsResponse data, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_redis.IsConnected || expiration <= TimeSpan.Zero)
                return;

            var db = _redis.GetDatabase();
            var payload = JsonSerializer.Serialize(data);
            await db.StringSetAsync(BuildStudentStatsKey(userId), payload, expiration);
        }
        catch
        {
        }
    }

    public async Task InvalidateStudentStatsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_redis.IsConnected)
                return;

            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(BuildStudentStatsKey(userId));
        }
        catch
        {
        }
    }

    private static string BuildStudentStatsKey(Guid userId) => $"{StudentStatsKeyPrefix}{userId:N}";
}
