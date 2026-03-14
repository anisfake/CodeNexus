using CodeNexus.Domain.Entities;

namespace CodeNexus.Application.Common.Interfaces;

public interface IProgressTrackingService
{
    Task<LearningPathProgressDto> CalculateLearningPathProgressAsync(
        Guid pathId, 
        Guid userId, 
        CancellationToken cancellationToken = default);

    Task<ChapterProgressDto> CalculateChapterProgressAsync(
        Guid chapterId, 
        Guid userId, 
        CancellationToken cancellationToken = default);

    Task<TimelineStatusDto> GetTimelineStatusAsync(
        Guid pathId, 
        Guid userId, 
        CancellationToken cancellationToken = default);
}

public record LearningPathProgressDto(
    Guid PathId,
    string Title,
    DateTime StartDate,
    DateTime EndDate,
    int TotalDays,
    int DaysElapsed,
    int DaysRemaining,
    double OverallProgressPercentage,
    TimelineStatus Status,
    List<ChapterProgressDto> ChapterProgress
);

public record ChapterProgressDto(
    Guid ChapterId,
    string Title,
    DateTime? StartDate,
    DateTime? EndDate,
    int EstimatedDays,
    double ProgressPercentage,
    TimelineStatus Status,
    int CompletedLessons,
    int TotalLessons,
    int CompletedTasks,
    int TotalTasks,
    int CompletedQuizzes,
    int TotalQuizzes
);

public record TimelineStatusDto(
    TimelineStatus OverallStatus,
    int DaysAhead,
    int DaysBehind,
    List<string> Recommendations
);

public enum TimelineStatus
{
    OnTrack,
    Ahead,
    Behind,
    AtRisk
}