using CodeNexus.Application.Common.Models;

namespace CodeNexus.Application.Common.Interfaces;

public interface IPlanUsageLimitService
{
    Task<Result> CheckLearningPathCreationAllowedAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result> CheckTutorMessageAllowedAsync(Guid userId, CancellationToken cancellationToken = default);
}
