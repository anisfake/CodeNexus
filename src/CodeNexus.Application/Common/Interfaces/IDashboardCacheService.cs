using CodeNexus.Application.Features.Dashboard.DTOs;

namespace CodeNexus.Application.Common.Interfaces;

public interface IDashboardCacheService
{
    Task<StudentDashboardStatsResponse?> GetStudentStatsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task SetStudentStatsAsync(Guid userId, StudentDashboardStatsResponse data, TimeSpan expiration, CancellationToken cancellationToken = default);
    Task InvalidateStudentStatsAsync(Guid userId, CancellationToken cancellationToken = default);
}
