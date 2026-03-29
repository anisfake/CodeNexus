namespace CodeNexus.Application.Features.Notifications.DTOs;

public record CreateOverdueNotificationsResultDto(
    int TotalCandidates,
    int CreatedCount,
    int TaskOverdueCount,
    int LearningPathOverdueCount,
    int ChapterOverdueCount,
    int LessonOverdueCount,
    int PlanExpiringSoonCount,
    int PlanExpiredCount
);
