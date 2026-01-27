using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Auth.DTOs;
using CodeNexus.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Auth.Commands.VerifyOtp;

public class VerifyOtpCommandHandler : IRequestHandler<VerifyOtpCommand, Result<UserDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IOTPService _optService;
    private const int MaxAttempts = 5;

    public VerifyOtpCommandHandler(IApplicationDbContext context, IOTPService optService)
    {
        _context = context;
        _optService = optService;
    }

    public async Task<Result<UserDto>> Handle(VerifyOtpCommand request, CancellationToken cancellationToken)
    {
        var otpVerification = await _context.OtpVerification
            .FirstOrDefaultAsync(o => o.Email == request.Email, cancellationToken);

        if (otpVerification == null)
        {
            return Result<UserDto>.Failure("INVALID_OTP", "No pending verification found for this email");
        }

        if (otpVerification.AttemptCount >= MaxAttempts)
        {
            _context.OtpVerification.Remove(otpVerification);
            await _context.SaveChangesAsync(cancellationToken);
            return Result<UserDto>.Failure("MAX_ATTEMPTS_EXCEEDED", "Too many failed attempts. Please request a new OTP");
        }

        if (DateTime.UtcNow > otpVerification.ExpiresAt)
        {
            return Result<UserDto>.Failure("OTP_EXPIRED", "OTP has expired. Please request a new one");
        }

        if (!_optService.VerifyOtp(request.Otp, otpVerification.OtpHash))
        {
            otpVerification.AttemptCount++;
            await _context.SaveChangesAsync(cancellationToken);
            return Result<UserDto>.Failure("INVALID_OTP", "Invalid OTP code");
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Email = otpVerification.Email,
            Username = otpVerification.Username,
            PasswordHash = otpVerification.PasswordHash,
            FirstName = otpVerification.FirstName,
            LastName = otpVerification.LastName,
            CreatedAt = now,
            Status = "Active"
        };

        _context.Users.Add(user);

        _context.OtpVerification.Remove(otpVerification);

        await _context.SaveChangesAsync(cancellationToken);

        var userDto = new UserDto(
            user.UserId,
            user.Email,
            user.Username,
            user.CreatedAt
        );

        return Result<UserDto>.Success(userDto);
    }
}
