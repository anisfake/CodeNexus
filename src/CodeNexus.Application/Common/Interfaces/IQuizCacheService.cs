using CodeNexus.Application.Features.Quizzes.DTOs;

namespace CodeNexus.Application.Common.Interfaces;

public interface IQuizCacheService
{
    Task<QuizStatusDto?> GetQuizStatusAsync(Guid quizId, Guid userId, CancellationToken cancellationToken = default);
    Task SetQuizStatusAsync(Guid quizId, Guid userId, QuizStatusDto status, TimeSpan expiration, CancellationToken cancellationToken = default);
    Task InvalidateQuizStatusAsync(Guid quizId, Guid userId, CancellationToken cancellationToken = default);
}
