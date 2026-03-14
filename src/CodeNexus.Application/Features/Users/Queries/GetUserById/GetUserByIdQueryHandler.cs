using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Users.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Users.Queries.GetUserById;

public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, Result<UserRespone>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUserCacheService _userCacheService;

    public GetUserByIdQueryHandler(IApplicationDbContext context, IUserCacheService userCacheService)
    {
        _context = context;
        _userCacheService = userCacheService;
    }

    public async Task<Result<UserRespone>> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var cachedUser = await _userCacheService.GetUserByIdAsync(request.UserId, cancellationToken);
        if (cachedUser != null)
        {
            return Result<UserRespone>.Success(cachedUser);
        }

        var user = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.UserProfile)
            .FirstOrDefaultAsync(u => u.UserId == request.UserId, cancellationToken);

        if (user == null)
        {
            return Result<UserRespone>.Failure("USER_NOT_FOUND", "User not found.");
        }

        var userResponse = new UserRespone(
            user.UserId,
            user.Username,
            user.Email,
            user.FirstName,
            user.LastName,
            user.UserProfile?.AvatarUrl,
            user.UserProfile?.Bio,
            user.UserProfile?.Phone,
            user.UserProfile?.Address,
            user.UserProfile?.DateOfBirth,
            user.LastLogin,
            user.Status,
            user.Role?.RoleName,
            user.CreatedAt
        );

        await _userCacheService.SetUserByIdAsync(request.UserId, userResponse, TimeSpan.FromMinutes(5), cancellationToken);

        return Result<UserRespone>.Success(userResponse);
    }
}
