using CodeNexus.Application.Features.Notifications.DTOs;

namespace CodeNexus.Application.Common.Interfaces;

public interface INotificationRealtimeNotifier
{
    Task NotifyCreatedAsync(IReadOnlyCollection<NotificationRealtimeDto> notifications, CancellationToken cancellationToken = default);
}
