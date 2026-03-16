using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.DirectChats.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.DirectChats.Queries.GetUnreadCount;

public class GetUnreadCountQueryHandler : IRequestHandler<GetUnreadCountQuery, Result<DirectUnreadCountDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetUnreadCountQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<DirectUnreadCountDto>> Handle(GetUnreadCountQuery request, CancellationToken cancellationToken)
    {
        Guid currentUserId;
        try
        {
            currentUserId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<DirectUnreadCountDto>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        var unreadCount = await _context.DirectMessageReceipts
            .AsNoTracking()
            .CountAsync(r => r.UserId == currentUserId && !r.SeenAt.HasValue, cancellationToken);

        return Result<DirectUnreadCountDto>.Success(new DirectUnreadCountDto(unreadCount));
    }
}
