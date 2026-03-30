using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Auth.DTOs;
using CodeNexus.Domain.Entities;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Auth.Commands.RefreshAccessToken;

public class RefreshAccessTokenCommandHandler : IRequestHandler<RefreshAccessTokenCommand, Result<LoginResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ITokenService _tokenService;

    public RefreshAccessTokenCommandHandler(IApplicationDbContext context, ITokenService tokenService)
    {
        _context = context;
        _tokenService = tokenService;
    }

    public async Task<Result<LoginResponse>> Handle(RefreshAccessTokenCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var hashedRequestToken = _tokenService.HashRefreshToken(request.RefreshToken);

        var storedToken = await _context.RefreshTokens
            .Include(rt => rt.User)
                .ThenInclude(u => u.Role)
            .FirstOrDefaultAsync(rt => rt.Token == hashedRequestToken, cancellationToken);

        if (storedToken == null)
            return Result<LoginResponse>.Failure("INVALID_REFRESH_TOKEN", "Invalid refresh token");

        if (storedToken.RevokedAt != null)
        {
            // Token reuse detected — possible token theft
            // Revoke ALL refresh tokens for this user to kill hacker's session
            var allActiveTokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == storedToken.UserId && rt.RevokedAt == null)
                .ToListAsync(cancellationToken);

            foreach (var token in allActiveTokens)
            {
                token.RevokedAt = now;
            }

            _context.SetAuditUserId(storedToken.UserId);
            await _context.SaveChangesAsync(cancellationToken);

            return Result<LoginResponse>.Failure("TOKEN_REUSE_DETECTED", "Token reuse detected. All sessions have been revoked for security.");
        }

        if (storedToken.ExpiresAt <= now)
            return Result<LoginResponse>.Failure("TOKEN_EXPIRED", "Refresh token has expired");

        var user = storedToken.User;

        storedToken.RevokedAt = now;

        var accessToken = _tokenService.GenerateAccessToken(user);

        var newRefreshTokenValue = _tokenService.GenerateRefreshToken();
        _context.RefreshTokens.Add(new RefreshToken
        {
            TokenId = NewId.NextGuid(),
            UserId = user.UserId,
            Token = _tokenService.HashRefreshToken(newRefreshTokenValue),
            CreatedAt = now,
            ExpiresAt = now.AddDays(_tokenService.RefreshTokenExpirationDays)
        });

        _context.SetAuditUserId(user.UserId);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<LoginResponse>.Success(new LoginResponse(
            accessToken,
            newRefreshTokenValue,
            user.UserId,
            user.Email,
            user.Username,
            user.LastLogin,
            user.RoleId,
            user.Role?.RoleName
        ));
    }
}
