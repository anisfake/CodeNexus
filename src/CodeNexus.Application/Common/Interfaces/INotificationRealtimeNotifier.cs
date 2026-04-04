using CodeNexus.Application.Features.Notifications.DTOs;

namespace CodeNexus.Application.Common.Interfaces;

public interface INotificationRealtimeNotifier
{
    Task NotifyCreatedAsync(IReadOnlyCollection<NotificationDto> notifications, CancellationToken cancellationToken = default);
}
