using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Notifications.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Notifications.Commands.MarkNotificationAsRead;

public class MarkNotificationAsReadCommandHandler : IRequestHandler<MarkNotificationAsReadCommand, Result<MarkNotificationAsReadResultDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public MarkNotificationAsReadCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<MarkNotificationAsReadResultDto>> Handle(MarkNotificationAsReadCommand request, CancellationToken cancellationToken)
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

        var notificationIds = request.NotificationIds
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray();

        var readAt = DateTime.UtcNow;
        var markedCount = 0;

        if (notificationIds.Length > 0)
        {
            var notifications = await _context.Notifications
                .Where(x => x.UserId == userId && notificationIds.Contains(x.NotificationId))
                .ToListAsync(cancellationToken);

            foreach (var notification in notifications)
            {
                if (notification.IsRead)
                {
                    continue;
                }

                notification.IsRead = true;
                notification.ReadAt = readAt;
                markedCount++;
            }

            if (markedCount > 0)
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        var unreadCount = await _context.Notifications
            .AsNoTracking()
            .CountAsync(x => x.UserId == userId && !x.IsRead, cancellationToken);

        return Result<MarkNotificationAsReadResultDto>.Success(new MarkNotificationAsReadResultDto(
            notificationIds,
            markedCount,
            markedCount > 0 ? readAt : null,
            unreadCount));
    }
}
