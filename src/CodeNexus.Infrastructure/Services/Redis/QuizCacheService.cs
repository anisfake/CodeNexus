using System.Text.Json;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Quizzes.DTOs;
using StackExchange.Redis;

namespace CodeNexus.Infrastructure.Services;

public class QuizCacheService : IQuizCacheService
{
    private readonly IConnectionMultiplexer _redis;
    private const string KeyPrefix = "quiz:status:";

    public QuizCacheService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<QuizStatusDto?> GetQuizStatusAsync(Guid quizId, Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_redis.IsConnected)
                return null;

            var db = _redis.GetDatabase();
            var key = BuildKey(quizId, userId);
            var value = await db.StringGetAsync(key);

            if (!value.HasValue)
                return null;

            return JsonSerializer.Deserialize<QuizStatusDto>(value!);
        }
        catch
        {
            return null;
        }
    }

    public async Task SetQuizStatusAsync(Guid quizId, Guid userId, QuizStatusDto status, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_redis.IsConnected)
                return;

            var db = _redis.GetDatabase();
            var key = BuildKey(quizId, userId);
            var payload = JsonSerializer.Serialize(status);

            await db.StringSetAsync(key, payload, expiration);
        }
        catch
        {
        }
    }

    public async Task InvalidateQuizStatusAsync(Guid quizId, Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_redis.IsConnected)
                return;

            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(BuildKey(quizId, userId));
        }
        catch
        {
        }
    }

    private static string BuildKey(Guid quizId, Guid userId)
    {
        return $"{KeyPrefix}{quizId}:{userId}";
    }
}
