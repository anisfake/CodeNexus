using FluentValidation;

namespace CodeNexus.Application.Features.Profile.Commands.ChangePassword;

public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    private const string PasswordPattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$";

    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required")
            .WithErrorCode("INVALID_USER_ID");

        RuleFor(x => x.CurrentPassword)
            .NotEmpty()
            .WithMessage("Current password is required")
            .WithErrorCode("INVALID_CURRENT_PASSWORD");

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage("New password is required")
            .WithErrorCode("INVALID_NEW_PASSWORD")
            .Matches(PasswordPattern)
            .WithMessage("New password must be at least 8 characters and contain at least 1 uppercase letter, 1 lowercase letter, and 1 number")
            .WithErrorCode("INVALID_NEW_PASSWORD");

        RuleFor(x => x)
            .Must(x => x.NewPassword != x.CurrentPassword)
            .WithMessage("New password must be different from current password")
            .WithErrorCode("PASSWORD_SAME_AS_CURRENT")
            .When(x => !string.IsNullOrEmpty(x.CurrentPassword) && !string.IsNullOrEmpty(x.NewPassword));
    }
}
