using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.Notifications.DTOs;

public record NotificationDto(
    Guid NotificationId,
    Guid UserId,
    NotificationType? Type,
    string Title,
    string? Message,
    DateTime CreatedAt,
    bool IsRead,
    DateTime? ReadAt,
    string Severity,
    IReadOnlyList<string> Channels,
    NotificationActionDto Action,
    string? NotifiedPathTitle,
    decimal? NotifiedSourceVersion,
    string? NotifiedMentorUserName
);
