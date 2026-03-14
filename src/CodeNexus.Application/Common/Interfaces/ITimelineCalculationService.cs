using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Common.Interfaces;

public interface ITimelineCalculationService
{
    Task<List<ChapterTimelineDto>> CalculateChapterTimelinesAsync(
        DateTime learningPathStartDate,
        DateTime learningPathEndDate,
        int totalChapters,
        ComplexityLevel complexity,
        CancellationToken cancellationToken = default);

    Task<List<LessonScheduleDto>> CalculateLessonSchedulesAsync(
        DateTime chapterStartDate,
        DateTime chapterEndDate,
        int totalLessons,
        ComplexityLevel complexity,
        CancellationToken cancellationToken = default);

    Task<List<TaskScheduleDto>> CalculateTaskSchedulesAsync(
        DateTime chapterStartDate,
        DateTime chapterEndDate,
        int totalTasks,
        ComplexityLevel complexity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculate quiz schedules for lessons
    /// </summary>
    Task<List<QuizScheduleDto>> CalculateQuizSchedulesAsync(
        List<LessonScheduleDto> lessonSchedules,
        int totalQuizzes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get number of quizzes per lesson based on complexity
    /// </summary>
    int GetQuizzesPerLesson(ComplexityLevel complexity);
}

public record ChapterTimelineDto(
    int OrderIndex,
    DateTime StartDate,
    DateTime EndDate,
    int EstimatedDays
);

public record LessonScheduleDto(
    int OrderIndex,
    DateTime LessonDay // Changed from ScheduledDate to LessonDay, removed EstimatedMinutes
);

public record TaskScheduleDto(
    int OrderIndex,
    DateTime DueDate
);

public record QuizScheduleDto(
    int LessonOrderIndex,
    DateTime DueDate
);