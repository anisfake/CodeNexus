using FluentValidation;

namespace CodeNexus.Application.Features.Auth.Commands.VerifyOtp;

public class VerifyOtpCommandValidator : AbstractValidator<VerifyOtpCommand>
{
    private const string EmailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
    private const string OtpPattern = @"^\d{6}$";

    public VerifyOtpCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .WithErrorCode("INVALID_EMAIL_FORMAT")
            .Matches(EmailPattern)
            .WithMessage("Email format is invalid")
            .WithErrorCode("INVALID_EMAIL_FORMAT");

        RuleFor(x => x.Otp)
            .NotEmpty()
            .WithMessage("OTP is required")
            .WithErrorCode("INVALID_OTP")
            .Matches(OtpPattern)
            .WithMessage("OTP must be exactly 6 digits")
            .WithErrorCode("INVALID_OTP");
    }
}
