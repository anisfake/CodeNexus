using FluentValidation;

namespace CodeNexus.Application.Features.Auth.Commands.Register;

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    private const string EmailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";

    // Password must have: 8+ chars, 1 uppercase, 1 lowercase, 1 number
    private const string PasswordPattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$";

    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .WithErrorCode("INVALID_EMAIL_FORMAT")
            .Matches(EmailPattern)
            .WithMessage("Email format is invalid")
            .WithErrorCode("INVALID_EMAIL_FORMAT");

        RuleFor(x => x.Username)
            .NotEmpty()
            .WithMessage("Username is required")
            .WithErrorCode("INVALID_USERNAME")
            .MinimumLength(3)
            .WithMessage("Username must be at least 3 characters")
            .WithErrorCode("INVALID_USERNAME")
            .MaximumLength(50)
            .WithMessage("Username must not exceed 50 characters")
            .WithErrorCode("INVALID_USERNAME");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Password is required")
            .WithErrorCode("INVALID_PASSWORD")
            .Matches(PasswordPattern)
            .WithMessage("Password must be at least 8 characters and contain at least 1 uppercase letter, 1 lowercase letter, and 1 number")
            .WithErrorCode("INVALID_PASSWORD");
    }
}
