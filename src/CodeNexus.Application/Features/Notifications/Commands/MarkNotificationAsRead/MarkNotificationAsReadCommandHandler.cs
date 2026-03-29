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

        var notification = await _context.Notifications
            .FirstOrDefaultAsync(x => x.NotificationId == request.NotificationId && x.UserId == userId, cancellationToken);

        if (notification == null)
        {
            return Result<MarkNotificationAsReadResultDto>.Failure("NOTIFICATION_NOT_FOUND", "Notification not found.");
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        var unreadCount = await _context.Notifications
            .AsNoTracking()
            .CountAsync(x => x.UserId == userId && !x.IsRead, cancellationToken);

        return Result<MarkNotificationAsReadResultDto>.Success(new MarkNotificationAsReadResultDto(
            notification.NotificationId,
            notification.IsRead,
            notification.ReadAt,
            unreadCount));
    }
}
