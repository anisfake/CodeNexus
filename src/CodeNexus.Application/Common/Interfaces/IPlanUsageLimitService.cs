using CodeNexus.Application.Common.Models;

namespace CodeNexus.Application.Common.Interfaces;

public interface IPlanUsageLimitService
{
    Task<Result> CheckLearningPathCreationAllowedAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result> CheckTutorMessageAllowedAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result> CheckFocusSessionReviewAllowedAsync(Guid userId, CancellationToken cancellationToken = default);
    Task RecordLearningPathCreationUsageAsync(Guid userId, CancellationToken cancellationToken = default);
    Task RecordTutorMessageUsageAsync(Guid userId, CancellationToken cancellationToken = default);
    Task RecordFocusSessionReviewUsageAsync(Guid userId, CancellationToken cancellationToken = default);
}
