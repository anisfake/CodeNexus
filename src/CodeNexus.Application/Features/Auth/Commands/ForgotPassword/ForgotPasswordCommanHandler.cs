using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Domain.Entities;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.Auth.Commands.ForgotPassword;

public class ForgotPasswordCommanHandler : IRequestHandler<ForgotPasswordCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IOTPService _otpService;
    private readonly IEmailService _emailService;
    private const int OtpExpirationMinutes = 5;
    private const int MinResendIntervalMinutes = 1;

    public ForgotPasswordCommanHandler(IApplicationDbContext context, IOTPService otpService, IEmailService emailService)
    {
        _context = context;
        _otpService = otpService;
        _emailService = emailService;
    }

    public async Task<Result> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        if (user == null)
        {
            return Result.Failure("USER_NOT_FOUND", "User not found!");
        }

        var now = DateTime.Now;

        var existingOtp = await _context.OtpVerification
            .Where(o => o.Email == request.Email && o.Purpose == OtpPurpose.ResetPassword)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingOtp != null)
        {
            if (existingOtp.LastResendAt.HasValue &&
                now - existingOtp.LastResendAt.Value < TimeSpan.FromMinutes(MinResendIntervalMinutes))
            {
                return Result.Failure("OTP_RATE_LIMITED", "Please wait 1 minute before requesting a new OTP");
            }

            _context.OtpVerification.Remove(existingOtp);
        }

        var otp = _otpService.GenerateOtp();
        var otpHash = _otpService.HashOtp(otp);

        var otpVerification = new OtpVerification
        {
            Id = NewId.NextGuid(),
            Email = request.Email,
            Username = string.Empty,
            PasswordHash = string.Empty,
            OtpHash = otpHash,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(OtpExpirationMinutes),
            AttemptCount = 0,
            ResendCount = 0,
            LastResendAt = now,
            FirstName = string.Empty,
            LastName = string.Empty,
            Purpose = OtpPurpose.ResetPassword
        };

        _context.OtpVerification.Add(otpVerification);
        await _context.SaveChangesAsync(cancellationToken);

        await _emailService.SendOtpEmailAsync(request.Email, otp, cancellationToken);

        return Result.Success();
    }
}
