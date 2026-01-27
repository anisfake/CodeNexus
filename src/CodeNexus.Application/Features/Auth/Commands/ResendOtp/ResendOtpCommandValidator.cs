using FluentValidation;

namespace CodeNexus.Application.Features.Auth.Commands.ResendOtp;

public class ResendOtpCommandValidator : AbstractValidator<ResendOtpCommand>
{
    private const string EmailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";

    public ResendOtpCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .WithErrorCode("INVALID_EMAIL_FORMAT")
            .Matches(EmailPattern)
            .WithMessage("Email format is invalid")
            .WithErrorCode("INVALID_EMAIL_FORMAT");
    }
}
