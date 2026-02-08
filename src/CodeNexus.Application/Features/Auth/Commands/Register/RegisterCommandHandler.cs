using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.Auth.Commands.Register;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IOTPService _otpService;
    private const int OtpExpirationMinutes = 5;

    public RegisterCommandHandler(
        IApplicationDbContext context,
        IEmailService emailService, IOTPService otpService)
    {
        _context = context;
        _emailService = emailService;
        _otpService = otpService;
    }

    public async Task<Result> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var emailExists = await _context.Users
            .AnyAsync(u => u.Email == request.Email, cancellationToken);

        if (emailExists)
        {
            return Result.Failure("EMAIL_EXISTS", "Email already registered");
        }

        var usernameExists = await _context.Users
            .AnyAsync(u => u.Username == request.Username, cancellationToken);

        if (usernameExists)
        {
            return Result.Failure("USERNAME_EXISTS", "Username already taken");
        }

        var existingOtp = await _context.OtpVerification
            .FirstOrDefaultAsync(o => o.Email == request.Email, cancellationToken);

        var now = DateTime.Now;

        if (existingOtp != null)
        {
            if (existingOtp.LastResendAt.HasValue &&
                now - existingOtp.LastResendAt.Value < TimeSpan.FromMinutes(1))
            {
                return Result.Failure("OTP_RATE_LIMITED", "Please wait 1 minute before requesting a new OTP");
            }

            _context.OtpVerification.Remove(existingOtp);
        }

        var otp = _otpService.GenerateOtp();
        var otpHash = _otpService.HashOtp(otp);

        var passwordHash = _otpService.HashPassword(request.Password);
        var otpVerification = new OtpVerification
        {
            Id = NewId.NextGuid(),
            Email = request.Email,
            Username = request.Username,
            PasswordHash = passwordHash,
            OtpHash = otpHash,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(OtpExpirationMinutes),
            AttemptCount = 0,
            ResendCount = 0,
            LastResendAt = now,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Purpose = OtpPurpose.Register
        };

        _context.OtpVerification.Add(otpVerification);

        await _context.SaveChangesAsync(cancellationToken);

        await _emailService.SendOtpEmailAsync(request.Email, otp, cancellationToken);

        return Result.Success();
    }
}
