using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Auth.DTOs;
using CodeNexus.Domain.Entities;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Auth.Commands.LoginWithGoogle;

public class LoginWithGoogleCommandHandler : IRequestHandler<LoginWithGoogleCommand, Result<LoginResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IGoogleAuthService _googleAuth;
    private readonly ITokenService _tokenService;

    public LoginWithGoogleCommandHandler(
        IApplicationDbContext context,
        IGoogleAuthService googleAuth,
        ITokenService tokenService)
    {
        _context = context;
        _googleAuth = googleAuth;
        _tokenService = tokenService;
    }

    public async Task<Result<LoginResponse>> Handle(LoginWithGoogleCommand request, CancellationToken cancellationToken)
    {
        var googleUser = await _googleAuth.ValidateIdTokenAsync(request.IdToken, cancellationToken);
        if (googleUser == null)
            return Result<LoginResponse>.Failure("INVALID_GOOGLE_TOKEN", "Invalid Google token");

        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == googleUser.Email, cancellationToken);

        if (user == null)
        {
            var username = googleUser.Email.Split('@')[0];
            var finalUsername = await EnsureUniqueUsernameAsync(username, cancellationToken);

            var defaultRole = await _context.Roles
                .FirstOrDefaultAsync(r => r.RoleName == "Student", cancellationToken);

            user = new User
            {
                UserId = NewId.NextGuid(),
                Email = googleUser.Email,
                Username = finalUsername,
                PasswordHash = string.Empty,
                FirstName = googleUser.GivenName,
                LastName = googleUser.FamilyName,
                CreatedAt = DateTime.Now,
                Status = "Active",
                RoleId = defaultRole?.RoleId
            };

            _context.Users.Add(user);

            var userProfile = new UserProfile
            {
                ProfileId = NewId.NextGuid(),
                UserId = user.UserId
            };

            _context.UserProfiles.Add(userProfile);
            await _context.SaveChangesAsync(cancellationToken);

            user.Role = defaultRole;
        }

        user.LastLogin = DateTime.Now;

        var accessToken = _tokenService.GenerateAccessToken(user);

        var refreshTokenValue = _tokenService.GenerateRefreshToken();
        _context.RefreshTokens.Add(new RefreshToken
        {
            TokenId = NewId.NextGuid(),
            UserId = user.UserId,
            Token = refreshTokenValue,
            CreatedAt = DateTime.Now,
            ExpiresAt = DateTime.Now.AddDays(7)
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

    private async Task<string> EnsureUniqueUsernameAsync(string baseUsername, CancellationToken cancellationToken)
    {
        var candidate = baseUsername;
        var suffix = 0;

        while (await _context.Users.AsNoTracking().AnyAsync(u => u.Username == candidate, cancellationToken))
        {
            suffix++;
            candidate = $"{baseUsername}{suffix}";
        }

        return candidate;
    }
}
