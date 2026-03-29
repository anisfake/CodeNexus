using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Notifications.Commands.MarkNotificationAsRead;

public class MarkNotificationAsReadCommandHandler : IRequestHandler<MarkNotificationAsReadCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public MarkNotificationAsReadCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(MarkNotificationAsReadCommand request, CancellationToken cancellationToken)
    {
        Guid userId;
        try
        {
            userId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result.Failure("UNAUTHORIZED", "User not authenticated");
        }

        var notification = await _context.Notifications
            .FirstOrDefaultAsync(x => x.NotificationId == request.NotificationId && x.UserId == userId, cancellationToken);

        if (notification == null)
        {
            return Result.Failure("NOTIFICATION_NOT_FOUND", "Notification not found.");
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
