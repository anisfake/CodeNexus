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
    private readonly IDailyReminderTimeInferenceService _dailyReminderTimeInferenceService;

    public LoginCommandHandler(
        IApplicationDbContext context,
        IPasswordService passwordService,
        ITokenService tokenService,
        IDailyReminderTimeInferenceService dailyReminderTimeInferenceService)
    {
        _context = context;
        _passwordService = passwordService;
        _tokenService = tokenService;
        _dailyReminderTimeInferenceService = dailyReminderTimeInferenceService;
    }

    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var identifier = request.Identifier.Trim();

        var user = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.UserProfile)
            .FirstOrDefaultAsync(u => u.Email == identifier || u.Username == identifier, cancellationToken);

        if (user == null)
            return Result<LoginResponse>.Failure("INVALID_CREDENTIALS", "Invalid username/email or password");

        if (user.Status == "Banned")
            return Result<LoginResponse>.Failure("USER_BANNED", "Your account has been banned. Please contact support.");

        if (!_passwordService.VerifyPassword(request.Password, user.PasswordHash))
            return Result<LoginResponse>.Failure("INVALID_CREDENTIALS", "Invalid username/email or password");

        var now = DateTime.UtcNow;
        var isFirstSuccessfulLogin = user.LastLogin == null;

        var expiredBlacklistedTokens = await _context.TokenBlacklist
            .Where(t => t.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

        if (expiredBlacklistedTokens.Any())
        {
            _context.TokenBlacklist.RemoveRange(expiredBlacklistedTokens);
        }

        var shouldPromptDailyReminderTime = isFirstSuccessfulLogin && user.UserProfile?.DailyReminderTime == null;
        if (user.UserProfile is { DailyReminderTime: null })
        {
            user.UserProfile.DailyReminderTime = await _dailyReminderTimeInferenceService
                .InferDailyReminderTimeAsync(user.UserId, cancellationToken);
        }

        user.LastLogin = now;

        var accessToken = _tokenService.GenerateAccessToken(user);

        var refreshTokenValue = _tokenService.GenerateRefreshToken();
        _context.RefreshTokens.Add(new RefreshToken
        {
            TokenId = NewId.NextGuid(),
            UserId = user.UserId,
            Token = _tokenService.HashRefreshToken(refreshTokenValue),
            CreatedAt = now,
            ExpiresAt = now.AddDays(_tokenService.RefreshTokenExpirationDays)
        });

        _context.SetAuditUserId(user.UserId);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<LoginResponse>.Success(new LoginResponse(
            accessToken,
            refreshTokenValue,
            user.UserId,
            user.Email,
            user.Username,
            user.LastLogin,
            user.RoleId,
            user.Role?.RoleName,
            shouldPromptDailyReminderTime
        ));
    }
}
