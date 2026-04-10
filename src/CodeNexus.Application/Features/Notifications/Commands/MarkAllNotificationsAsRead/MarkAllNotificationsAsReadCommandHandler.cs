using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Notifications.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Notifications.Commands.MarkAllNotificationsAsRead;

public class MarkAllNotificationsAsReadCommandHandler : IRequestHandler<MarkAllNotificationsAsReadCommand, Result<MarkNotificationAsReadResultDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public MarkAllNotificationsAsReadCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<MarkNotificationAsReadResultDto>> Handle(MarkAllNotificationsAsReadCommand request, CancellationToken cancellationToken)
    {
        Guid userId;
        try
        {
            userId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<MarkNotificationAsReadResultDto>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        var unreadNotifications = await _context.Notifications
            .Where(x => x.UserId == userId && !x.IsRead)
            .ToListAsync(cancellationToken);

        var readAt = DateTime.UtcNow;
        var markedCount = 0;

        foreach (var notification in unreadNotifications)
        {
            notification.IsRead = true;
            notification.ReadAt = readAt;
            markedCount++;
        }

        if (markedCount > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        var unreadCount = await _context.Notifications
            .AsNoTracking()
            .CountAsync(x => x.UserId == userId && !x.IsRead, cancellationToken);

        return Result<MarkNotificationAsReadResultDto>.Success(new MarkNotificationAsReadResultDto(
            unreadNotifications.Select(x => x.NotificationId).ToArray(),
            markedCount,
            markedCount > 0 ? readAt : null,
            unreadCount));
    }
}
