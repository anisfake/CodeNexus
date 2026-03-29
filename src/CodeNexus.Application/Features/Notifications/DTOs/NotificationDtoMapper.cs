using CodeNexus.Domain.Entities;

namespace CodeNexus.Application.Features.Notifications.DTOs;

public static class NotificationDtoMapper
{
    public static NotificationDto ToDto(Notification notification)
    {
        var channels = (notification.Channels ?? "Web")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (channels.Length == 0)
        {
            channels = ["Web"];
        }

        return new NotificationDto(
            notification.NotificationId,
            notification.UserId,
            notification.Type,
            notification.Title,
            notification.Message,
            DateTime.SpecifyKind(notification.CreatedAt, DateTimeKind.Utc),
            notification.IsRead,
            notification.ReadAt.HasValue ? DateTime.SpecifyKind(notification.ReadAt.Value, DateTimeKind.Utc) : null,
            string.IsNullOrWhiteSpace(notification.Severity) ? "Info" : notification.Severity,
            channels,
            new NotificationActionDto(
                notification.TargetType,
                notification.TargetId,
                notification.TargetUrl,
                notification.Route,
                notification.TaskId,
                notification.ChapterId,
                notification.LessonId,
                notification.LearningPathId)
        );
    }
}
