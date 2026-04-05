namespace CodeNexus.Application.Features.Notifications.DTOs;

public record MarkNotificationAsReadResultDto(
    IReadOnlyCollection<Guid> NotificationIds,
    int MarkedCount,
    DateTime? ReadAt,
    int UnreadCount
);
