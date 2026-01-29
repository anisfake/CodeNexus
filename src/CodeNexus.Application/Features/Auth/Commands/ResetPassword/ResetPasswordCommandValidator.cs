using FluentValidation;

namespace CodeNexus.Application.Features.Auth.Commands.ResetPassword;

public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    // Password must have: 8+ chars, 1 uppercase, 1 lowercase, 1 number
    private const string PasswordPattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$";

    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.ResetToken)
            .NotEmpty()
            .WithMessage("Reset token is required")
            .WithErrorCode("INVALID_TOKEN");

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage("Password is required")
            .WithErrorCode("INVALID_PASSWORD")
            .Matches(PasswordPattern)
            .WithMessage("Password must be at least 8 characters and contain at least 1 uppercase letter, 1 lowercase letter, and 1 number")
            .WithErrorCode("INVALID_PASSWORD");
    }
}
