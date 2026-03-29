using CodeNexus.API.Hubs;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Notifications.DTOs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.API.Services;

public class NotificationRealtimeNotifier : INotificationRealtimeNotifier
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly IApplicationDbContext _context;

    public NotificationRealtimeNotifier(IHubContext<NotificationHub> hubContext, IApplicationDbContext context)
    {
        _hubContext = hubContext;
        _context = context;
    }

    public async Task NotifyCreatedAsync(IReadOnlyCollection<NotificationDto> notifications, CancellationToken cancellationToken = default)
    {
        foreach (var group in notifications.GroupBy(x => x.UserId))
        {
            var userGroup = NotificationHub.GetUserGroup(group.Key.ToString());
            foreach (var notification in group)
            {
                await _hubContext.Clients.Group(userGroup)
                    .SendAsync("ReceiveNotification", notification, cancellationToken);
            }

            var unreadCount = await _context.Notifications
                .AsNoTracking()
                .CountAsync(x => x.UserId == group.Key && !x.IsRead, cancellationToken);

            await _hubContext.Clients.Group(userGroup)
                .SendAsync("NotificationUnreadCountChanged", new { unreadCount }, cancellationToken);
        }
    }
}
