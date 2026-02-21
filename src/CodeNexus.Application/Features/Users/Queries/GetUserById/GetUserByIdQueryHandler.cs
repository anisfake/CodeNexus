using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Users.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Users.Queries.GetUserById;

public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, Result<UserRespone>>
{
    private readonly IApplicationDbContext _context;

    public GetUserByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<UserRespone>> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
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
            user.Role?.RoleName,
            user.CreatedAt
        );

        return Result<UserRespone>.Success(userResponse);
    }
}
