using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Notifications.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Notifications.Queries.GetMyNotifications;

public class GetMyNotificationsQueryHandler : IRequestHandler<GetMyNotificationsQuery, Result<List<NotificationItemDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetMyNotificationsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<List<NotificationItemDto>>> Handle(GetMyNotificationsQuery request, CancellationToken cancellationToken)
    {
        Guid userId;
        try
        {
            userId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<List<NotificationItemDto>>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        var pageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber;
        var pageSize = request.PageSize <= 0 ? 20 : Math.Min(request.PageSize, 100);

        var query = _context.Notifications
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        if (request.UnreadOnly)
        {
            query = query.Where(x => !x.IsRead);
        }

        var notifications = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new NotificationItemDto(
                x.NotificationId,
                x.Title,
                x.Message,
                x.Type,
                x.IsRead,
                x.CreatedAt,
                x.ReadAt))
            .ToListAsync(cancellationToken);

        return Result<List<NotificationItemDto>>.Success(notifications);
    }
}
