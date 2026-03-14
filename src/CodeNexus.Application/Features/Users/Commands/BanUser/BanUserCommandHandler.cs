using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Users.Commands.BanUser;

public class BanUserCommandHandler : IRequestHandler<BanUserCommand, Result<string>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUserCacheService _userCacheService;

    public BanUserCommandHandler(IApplicationDbContext context, IUserCacheService userCacheService)
    {
        _context = context;
        _userCacheService = userCacheService;
    }

    public async Task<Result<string>> Handle(BanUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.UserId == request.UserId, cancellationToken);

        if (user == null)
        {
            return Result<string>.Failure("USER_NOT_FOUND", "User not found.");
        }

        if (user.Status == "Banned")
        {
            return Result<string>.Failure("USER_ALREADY_BANNED", "User is already banned.");
        }

        user.Status = "Banned";

        var activeTokens = user.RefreshTokens
            .Where(rt => rt.RevokedAt == null)
            .ToList();

        foreach (var token in activeTokens)
        {
            token.RevokedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
        await _userCacheService.InvalidateUserAsync(request.UserId, cancellationToken);

        return Result<string>.Success($"Successfully banned the user {user.Username}");
    }
}
