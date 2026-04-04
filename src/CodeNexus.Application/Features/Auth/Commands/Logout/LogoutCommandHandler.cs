using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Domain.Entities;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Auth.Commands.Logout;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITokenService _tokenService;

    public LogoutCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ITokenService tokenService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _tokenService = tokenService;
    }

    public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        Guid? userId = _currentUserService.GetUserId();

        if (!userId.HasValue || userId.Value == Guid.Empty)
            return Result.Failure("UNAUTHORIZED", "User not authenticated");

        Guid userIdValue = userId.Value;

        if (!string.IsNullOrWhiteSpace(request.AccessToken))
        {
            var tokenInfo = _tokenService.ExtractTokenInfo(request.AccessToken);
            string? tokenId = tokenInfo.Item1;
            DateTime? expiresAt = tokenInfo.Item2;

            if (!string.IsNullOrEmpty(tokenId) && expiresAt.HasValue)
            {
                var existingBlacklist = await _context.TokenBlacklist
                    .FirstOrDefaultAsync(t => t.TokenId == tokenId, cancellationToken);

                if (existingBlacklist == null)
                {
                    var blacklist = new TokenBlacklist
                    {
                        Id = NewId.NextGuid(),
                        TokenId = tokenId,
                        ExpiresAt = expiresAt.Value,
                        BlacklistedAt = DateTime.UtcNow,
                        Reason = "User logout"
                    };

                    _context.TokenBlacklist.Add(blacklist);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            var hashedRequestToken = _tokenService.HashRefreshToken(request.RefreshToken);

            var refreshToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == hashedRequestToken && rt.UserId == userIdValue, cancellationToken);

            if (refreshToken != null)
            {
                refreshToken.RevokedAt = DateTime.UtcNow;
            }
        }
        else
        {
            var activeTokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == userIdValue && rt.RevokedAt == null)
                .ToListAsync(cancellationToken);

            foreach (var token in activeTokens)
            {
                token.RevokedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
