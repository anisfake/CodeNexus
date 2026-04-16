using FluentValidation;

namespace CodeNexus.Application.Features.TokenPackages.Commands.CreateTokenPackage;

public class CreateTokenPackageCommandValidator : AbstractValidator<CreateTokenPackageCommand>
{
    public CreateTokenPackageCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(120);

        RuleFor(x => x.Description)
            .MaximumLength(500);

        RuleFor(x => x.PriceVnd)
            .GreaterThan(0);

        RuleFor(x => x.CreditedTokens)
            .GreaterThan(0);

        RuleFor(x => x.DisplayOrder)
            .GreaterThanOrEqualTo(0);
    }
}

