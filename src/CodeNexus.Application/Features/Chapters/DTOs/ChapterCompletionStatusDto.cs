namespace CodeNexus.Application.Features.Chapters.DTOs;

public record ChapterCompletionStatusDto(
    Guid ChapterId,
    bool IsCompleted,
    int CompletedLessons,
    int TotalLessons,
    int CompletedTasks,
    int TotalTasks,
    int CompletedQuizzes,
    int TotalQuizzes,
    decimal ProgressPercent
);
