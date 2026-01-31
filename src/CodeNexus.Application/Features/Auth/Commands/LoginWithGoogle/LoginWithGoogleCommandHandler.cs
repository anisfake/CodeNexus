using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Auth.DTOs;
using CodeNexus.Domain.Entities;
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
            .FirstOrDefaultAsync(u => u.Email == googleUser.Email, cancellationToken);

        if (user == null)
        {
            var username = googleUser.Email.Split('@')[0];
            var finalUsername = await EnsureUniqueUsernameAsync(username, cancellationToken);

            user = new User
            {
                UserId = Guid.NewGuid(),
                Email = googleUser.Email,
                Username = finalUsername,
                PasswordHash = string.Empty,
                FirstName = googleUser.GivenName,
                LastName = googleUser.FamilyName,
                CreatedAt = DateTime.UtcNow,
                Status = "Active"
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var accessToken = _tokenService.GenerateAccessToken(user);

        return Result<LoginResponse>.Success(new LoginResponse(
            accessToken,
            user.UserId,
            user.Email,
            user.Username
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
