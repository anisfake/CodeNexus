using CodeNexus.Application.Common.Constants;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Auth.DTOs;
using CodeNexus.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Auth.Commands.VerifyOtp;

public class VerifyOtpCommandHandler : IRequestHandler<VerifyOtpCommand, Result<VerifyOtpResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IOTPService _otpService;
    private readonly ITokenService _tokenService;
    private const int MaxAttempts = 5;

    public VerifyOtpCommandHandler(IApplicationDbContext context, IOTPService otpService, ITokenService tokenService)
    {
        _context = context;
        _otpService = otpService;
        _tokenService = tokenService;
    }

    public async Task<Result<VerifyOtpResponse>> Handle(VerifyOtpCommand request, CancellationToken cancellationToken)
    {
        var otpVerification = await _context.OtpVerification
            .FirstOrDefaultAsync(o => o.Email == request.Email, cancellationToken);

        if (otpVerification == null)
            return Result<VerifyOtpResponse>.Failure("INVALID_OTP", "No pending verification found for this email");

        if (otpVerification.AttemptCount >= MaxAttempts)
        {
            _context.OtpVerification.Remove(otpVerification);
            await _context.SaveChangesAsync(cancellationToken);
            return Result<VerifyOtpResponse>.Failure("MAX_ATTEMPTS_EXCEEDED", "Too many failed attempts. Please request a new OTP");
        }

        if (DateTime.Now > otpVerification.ExpiresAt)
        {
            _context.OtpVerification.Remove(otpVerification);
            await _context.SaveChangesAsync(cancellationToken);
            return Result<VerifyOtpResponse>.Failure("OTP_EXPIRED", "OTP has expired. Please request a new one");
        }

        if (!_otpService.VerifyOtp(request.Otp, otpVerification.OtpHash))
        {
            otpVerification.AttemptCount++;
            await _context.SaveChangesAsync(cancellationToken);
            return Result<VerifyOtpResponse>.Failure("INVALID_OTP", "Invalid OTP code");
        }

        var response = otpVerification.Purpose switch
        {
            OtpPurpose.Register => await HandleRegister(otpVerification, cancellationToken),
            OtpPurpose.ResetPassword => HandleResetPassword(otpVerification),
            _ => Result<VerifyOtpResponse>.Failure("INVALID_PURPOSE", "Invalid OTP purpose")
        };

        if (!response.IsSuccess)
            return response;

        _context.OtpVerification.Remove(otpVerification);
        await _context.SaveChangesAsync(cancellationToken);

        return response;
    }

    private async Task<Result<VerifyOtpResponse>> HandleRegister(OtpVerification otpVerification, CancellationToken cancellationToken)
    {
        var existingUser = await _context.Users
            .AnyAsync(u => u.Email == otpVerification.Email || u.Username == otpVerification.Username, cancellationToken);

        if (existingUser)
            return Result<VerifyOtpResponse>.Failure("USER_EXISTS", "User already exists");

        var user = new User
        {
            UserId = Guid.NewGuid(),
            Email = otpVerification.Email,
            Username = otpVerification.Username,
            PasswordHash = otpVerification.PasswordHash,
            FirstName = otpVerification.FirstName,
            LastName = otpVerification.LastName,
            CreatedAt = DateTime.UtcNow,
            Status = "Active"
        };

        _context.Users.Add(user);

        return Result<VerifyOtpResponse>.Success(new VerifyOtpResponse
        {
            Purpose = OtpPurpose.Register,
            Message = "Registration successful"
        });
    }

    private Result<VerifyOtpResponse> HandleResetPassword(OtpVerification otpVerification)
    {
        var resetToken = _tokenService.GenerateResetPasswordToken(otpVerification.Email);

        return Result<VerifyOtpResponse>.Success(new VerifyOtpResponse
        {
            Purpose = OtpPurpose.ResetPassword,
            ResetToken = resetToken,
            Message = "OTP verified. Use reset token to change password"
        });
    }
}
