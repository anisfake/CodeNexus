using CodeNexus.Domain.Entities;

namespace CodeNexus.Application.Features.LearningPathShares.Services;

public interface ILearningPathSharePathSyncService
{
    Task<Guid> ClonePathForStudentAsync(
        LearningPath sourcePath,
        Guid studentId,
        DateTime acceptedAt,
        CancellationToken cancellationToken);

    Task RebuildCurrentPathFromSourceAsync(
        LearningPath currentPath,
        LearningPath sourcePath,
        Guid studentId,
        DateTime now,
        CancellationToken cancellationToken,
        IReadOnlySet<string>? contentChangedLessonKeys = null);
}
