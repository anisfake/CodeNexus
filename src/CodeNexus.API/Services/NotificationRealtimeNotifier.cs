using CodeNexus.API.Hubs;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Notifications.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace CodeNexus.API.Services;

public class NotificationRealtimeNotifier : INotificationRealtimeNotifier
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public NotificationRealtimeNotifier(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyCreatedAsync(IReadOnlyCollection<NotificationRealtimeDto> notifications, CancellationToken cancellationToken = default)
    {
        foreach (var group in notifications.GroupBy(x => x.UserId))
        {
            var userGroup = NotificationHub.GetUserGroup(group.Key.ToString());
            foreach (var notification in group)
            {
                await _hubContext.Clients.Group(userGroup)
                    .SendAsync("ReceiveNotification", notification, cancellationToken);
            }

            await _hubContext.Clients.Group(userGroup)
                .SendAsync("NotificationUnreadCountChanged", new { }, cancellationToken);
        }
    }
}
