using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.Notifications.DTOs;

public record NotificationRealtimeDto(
    Guid NotificationId,
    Guid UserId,
    string Title,
    string? Message,
    NotificationType? Type,
    DateTime CreatedAt
);
