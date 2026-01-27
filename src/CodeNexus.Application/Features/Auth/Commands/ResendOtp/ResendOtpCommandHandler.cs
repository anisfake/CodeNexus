using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Auth.Commands.ResendOtp;

public class ResendOtpCommandHandler : IRequestHandler<ResendOtpCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IOTPService _otpService;
    private const int OtpExpirationMinutes = 5;
    private const int MinResendIntervalMinutes = 1;
    private const int MaxResendPerHour = 5;

    public ResendOtpCommandHandler(
        IApplicationDbContext context,
        IEmailService emailService,
        IOTPService otpService)
    {
        _context = context;
        _emailService = emailService;
        _otpService = otpService;
    }

    public async Task<Result> Handle(ResendOtpCommand request, CancellationToken cancellationToken)
    {
        var existingOtp = await _context.OtpVerification
            .FirstOrDefaultAsync(o => o.Email == request.Email, cancellationToken);

        if (existingOtp == null)
        {
            return Result.Failure("INVALID_EMAIL", "No pending verification found for this email");
        }

        if (existingOtp.LastResendAt.HasValue &&
            DateTime.UtcNow - existingOtp.LastResendAt.Value < TimeSpan.FromMinutes(MinResendIntervalMinutes))
        {
            return Result.Failure("OTP_RATE_LIMITED", "Please wait 1 minute before requesting a new OTP");
        }

        if (existingOtp.ResendCount >= MaxResendPerHour)
        {
            return Result.Failure("RESEND_RATE_LIMITED", "Too many resend requests. Please try again later");
        }

        var newOtp = _otpService.GenerateOtp();
        var newOtpHash = _otpService.HashOtp(newOtp);

        var now = DateTime.UtcNow;
        existingOtp.OtpHash = newOtpHash;
        existingOtp.ExpiresAt = now.AddMinutes(OtpExpirationMinutes);
        existingOtp.AttemptCount = 0;
        existingOtp.ResendCount++;
        existingOtp.LastResendAt = now;

        await _context.SaveChangesAsync(cancellationToken);

        await _emailService.SendOtpEmailAsync(request.Email, newOtp, cancellationToken);

        return Result.Success();
    }
}
