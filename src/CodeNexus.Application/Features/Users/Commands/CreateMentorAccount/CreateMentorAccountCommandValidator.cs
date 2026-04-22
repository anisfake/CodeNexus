using FluentValidation;

namespace CodeNexus.Application.Features.Users.Commands.CreateMentorAccount;

public class CreateMentorAccountCommandValidator : AbstractValidator<CreateMentorAccountCommand>
{
    private const string EmailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
    private const string UsernamePattern = @"^[a-zA-Z0-9._]+$";

    public CreateMentorAccountCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required.")
            .WithErrorCode("INVALID_EMAIL_FORMAT")
            .Matches(EmailPattern)
            .WithMessage("Email format is invalid.")
            .WithErrorCode("INVALID_EMAIL_FORMAT");

        RuleFor(x => x.Username)
            .MinimumLength(3)
            .WithMessage("Username must be at least 3 characters.")
            .WithErrorCode("INVALID_USERNAME")
            .MaximumLength(50)
            .WithMessage("Username must not exceed 50 characters.")
            .WithErrorCode("INVALID_USERNAME")
            .Matches(UsernamePattern)
            .WithMessage("Username can only contain letters, numbers, '.', and '_'.")
            .WithErrorCode("INVALID_USERNAME")
            .When(x => !string.IsNullOrWhiteSpace(x.Username));

        RuleFor(x => x.FirstName)
            .MaximumLength(100)
            .WithMessage("First name must not exceed 100 characters.")
            .WithErrorCode("INVALID_FIRST_NAME")
            .When(x => !string.IsNullOrWhiteSpace(x.FirstName));

        RuleFor(x => x.LastName)
            .MaximumLength(100)
            .WithMessage("Last name must not exceed 100 characters.")
            .WithErrorCode("INVALID_LAST_NAME")
            .When(x => !string.IsNullOrWhiteSpace(x.LastName));

        RuleFor(x => x.Bio)
            .MaximumLength(1000)
            .WithMessage("Bio must not exceed 1000 characters.")
            .WithErrorCode("INVALID_BIO")
            .When(x => !string.IsNullOrWhiteSpace(x.Bio));

        RuleFor(x => x.Phone)
            .MaximumLength(20)
            .WithMessage("Phone must not exceed 20 characters.")
            .WithErrorCode("INVALID_PHONE")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));

        RuleFor(x => x.Address)
            .MaximumLength(300)
            .WithMessage("Address must not exceed 300 characters.")
            .WithErrorCode("INVALID_ADDRESS")
            .When(x => !string.IsNullOrWhiteSpace(x.Address));

        RuleFor(x => x.DateOfBirth)
            .LessThanOrEqualTo(DateTime.UtcNow.Date)
            .WithMessage("Date of birth cannot be in the future.")
            .WithErrorCode("INVALID_DATE_OF_BIRTH")
            .When(x => x.DateOfBirth.HasValue);
    }
}
