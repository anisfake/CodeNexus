using CodeNexus.Domain.Enums;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Auth.DTOs;
using CodeNexus.Domain.Entities;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Auth.Commands.VerifyOtp;

public class VerifyOtpCommandHandler : IRequestHandler<VerifyOtpCommand, Result<VerifyOtpResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IOTPCacheService _otpCacheService;
    private readonly ITokenService _tokenService;

    public VerifyOtpCommandHandler(
        IApplicationDbContext context,
        IOTPCacheService otpCacheService,
        ITokenService tokenService)
    {
        _context = context;
        _otpCacheService = otpCacheService;
        _tokenService = tokenService;
    }

    public async Task<Result<VerifyOtpResponse>> Handle(VerifyOtpCommand request, CancellationToken cancellationToken)
    {
        var verifyResult = await _otpCacheService.VerifyOtpAsync(request.Email, request.Otp, cancellationToken);

        if (!verifyResult.IsSuccess)
        {
            return Result<VerifyOtpResponse>.Failure(verifyResult.ErrorCode, verifyResult.ErrorMessage);
        }

        var (data, purpose) = verifyResult.Value;

        Result<VerifyOtpResponse> response = purpose switch
        {
            OtpPurpose.Register => await HandleRegister(request.Email, data, cancellationToken),
            OtpPurpose.ResetPassword => await HandleResetPassword(request.Email, cancellationToken),
            _ => Result<VerifyOtpResponse>.Failure("INVALID_PURPOSE", "Invalid OTP purpose")
        };

        if (!response.IsSuccess)
            return response;

        await _otpCacheService.DeleteOtpDataAsync(request.Email, cancellationToken);

        return response;
    }

    private async Task<Result<VerifyOtpResponse>> HandleRegister(
        string email,
        string registrationData,
        CancellationToken cancellationToken)
    {
        var parts = registrationData.Split('|');

        if (parts.Length != 4)
        {
            return Result<VerifyOtpResponse>.Failure("INVALID_PURPOSE", "Invalid OTP purpose");
        }

        var username = parts[0];
        var passwordHash = parts[1];
        var firstName = parts[2];
        var lastName = parts[3];

        var existingUser = await _context.Users
            .AnyAsync(u => u.Email == email || u.Username == username, cancellationToken);

        if (existingUser)
            return Result<VerifyOtpResponse>.Failure("USER_EXISTS", "User already exists");

        var defaultRole = await _context.Roles
            .FirstOrDefaultAsync(r => r.RoleName.Equals("Student"), cancellationToken);

        var user = new User
        {
            UserId = NewId.NextGuid(),
            Email = email,
            Username = username,
            PasswordHash = passwordHash,
            FirstName = firstName,
            LastName = lastName,
            CreatedAt = DateTime.Now,
            RoleId = defaultRole?.RoleId
        };

        await _context.Users.AddAsync(user, cancellationToken);

        var userProfile = new UserProfile
        {
            ProfileId = NewId.NextGuid(),
            UserId = user.UserId
        };

        await _context.UserProfiles.AddAsync(userProfile, cancellationToken);
        _context.SetAuditUserId(user.UserId);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<VerifyOtpResponse>.Success(new VerifyOtpResponse(
            OtpPurpose.Register,
            null,
            "Registration successful"
        ));
    }

    private async Task<Result<VerifyOtpResponse>> HandleResetPassword(
        string email,
        CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user == null)
        {
            return Result<VerifyOtpResponse>.Failure("USER_NOT_FOUND", "User not found.");
        }

        var resetToken = _tokenService.GenerateResetPasswordToken(email);

        return Result<VerifyOtpResponse>.Success(new VerifyOtpResponse(
            OtpPurpose.ResetPassword,
            resetToken,
            "OTP verified successfully"
        ));
    }
}
