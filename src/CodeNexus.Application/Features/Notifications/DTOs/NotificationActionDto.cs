namespace CodeNexus.Application.Features.Notifications.DTOs;

public record NotificationActionDto(
    string? TargetType,
    Guid? TargetId,
    string? TargetUrl,
    string? Route,
    Guid? TaskId,
    Guid? ChapterId,
    Guid? LessonId,
    Guid? LearningPathId
);
