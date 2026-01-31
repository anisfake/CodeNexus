using FluentValidation;

namespace CodeNexus.Application.Features.Auth.Commands.Login;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Identifier)
            .NotEmpty()
            .WithMessage("Identifier is required")
            .WithErrorCode("IDENTIFIER_REQUIRED");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Password is required")
            .WithErrorCode("PASSWORD_REQUIRED");
    }
}
