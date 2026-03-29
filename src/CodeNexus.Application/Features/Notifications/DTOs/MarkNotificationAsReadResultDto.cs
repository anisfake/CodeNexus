namespace CodeNexus.Application.Features.Notifications.DTOs;

public record MarkNotificationAsReadResultDto(
    Guid NotificationId,
    bool IsRead,
    DateTime? ReadAt,
    int UnreadCount
);
