namespace CodeNexus.Application.Features.LearningPaths.DTOs;

public record LearningPathCompletionProgressDto(
    Guid PathId,
    int CompletedLessonContents,
    int TotalLessonContents,
    decimal ContentProgressPercent,
    int CompletedQuizzes,
    int TotalQuizzes,
    decimal QuizProgressPercent,
    int CompletedTasks,
    int TotalTasks,
    decimal ProgressPercent,
    string Status
);