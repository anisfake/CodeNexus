using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Auth.DTOs;
using CodeNexus.Domain.Entities;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Auth.Commands.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordService _passwordService;
    private readonly ITokenService _tokenService;

    public LoginCommandHandler(IApplicationDbContext context, IPasswordService passwordService, ITokenService tokenService)
    {
        _context = context;
        _passwordService = passwordService;
        _tokenService = tokenService;
    }

    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var identifier = request.Identifier.Trim();

        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == identifier || u.Username == identifier, cancellationToken);

        if (user == null)
            return Result<LoginResponse>.Failure("INVALID_CREDENTIALS", "Invalid credentials");

        if (!_passwordService.VerifyPassword(request.Password, user.PasswordHash))
            return Result<LoginResponse>.Failure("INVALID_CREDENTIALS", "Invalid credentials");

        var now = DateTime.Now;

        var expiredBlacklistedTokens = await _context.TokenBlacklist
            .Where(t => t.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

        if (expiredBlacklistedTokens.Any())
        {
            _context.TokenBlacklist.RemoveRange(expiredBlacklistedTokens);
        }

        var accessToken = _tokenService.GenerateAccessToken(user);

        var refreshTokenValue = _tokenService.GenerateRefreshToken();
        _context.RefreshTokens.Add(new RefreshToken
        {
            TokenId = NewId.NextGuid(),
            UserId = user.UserId,
            Token = refreshTokenValue,
            CreatedAt = now,
            ExpiresAt = now.AddDays(7)
        });

        await _context.SaveChangesAsync(cancellationToken);

        return Result<LoginResponse>.Success(new LoginResponse(
            accessToken,
            refreshTokenValue,
            user.UserId,
            user.Email,
            user.Username,
            user.RoleId,
            user.Role?.RoleName
        ));
    }
}
