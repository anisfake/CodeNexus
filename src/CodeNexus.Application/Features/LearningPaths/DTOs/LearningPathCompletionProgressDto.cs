namespace CodeNexus.Application.Features.LearningPaths.DTOs;

public record LearningPathCompletionProgressDto(
    Guid PathId,
    int CompletedQuizzes,
    int TotalQuizzes,
    int CompletedTasks,
    int TotalTasks,
    decimal ProgressPercent,
    string Status
);