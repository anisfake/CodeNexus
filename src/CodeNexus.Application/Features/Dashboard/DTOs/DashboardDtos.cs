namespace CodeNexus.Application.Features.Dashboard.DTOs;

public record StudentDashboardStatsResponse(
    int TotalLessons,
    int CompletedLessons,
    int TotalChapters,
    int CompletedChapters,
    int TotalLearningPaths,
    int TotalQuizAttempts,
    int TotalStudyMinutes,
    int CurrentStreak
);
