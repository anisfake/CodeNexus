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
    Task<List<QuizScheduleDto>> CalculateQuizSchedulesAsync(
        List<LessonScheduleDto> lessonSchedules,
        int totalQuizzes,
        CancellationToken cancellationToken = default);

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
    DateTime LessonDay
);

public record TaskScheduleDto(
    int OrderIndex,
    DateTime DueDate
);

public record QuizScheduleDto(
    int LessonOrderIndex,
    DateTime DueDate
);