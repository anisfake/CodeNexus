using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.Notifications.DTOs;

public record NotificationItemDto(
    Guid NotificationId,
    string Title,
    string? Message,
    NotificationType? Type,
    bool IsRead,
    DateTime CreatedAt,
    DateTime? ReadAt
);
