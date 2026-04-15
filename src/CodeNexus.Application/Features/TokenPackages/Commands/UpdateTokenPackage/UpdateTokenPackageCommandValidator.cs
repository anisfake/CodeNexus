using FluentValidation;

namespace CodeNexus.Application.Features.TokenPackages.Commands.UpdateTokenPackage;

public class UpdateTokenPackageCommandValidator : AbstractValidator<UpdateTokenPackageCommand>
{
    public UpdateTokenPackageCommandValidator()
    {
        RuleFor(x => x.TokenPackageId)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(120);

        RuleFor(x => x.Description)
            .MaximumLength(500);

        RuleFor(x => x.PriceVnd)
            .GreaterThan(0);

        RuleFor(x => x.CreditedBalanceVnd)
            .GreaterThan(0);

        RuleFor(x => x.DisplayOrder)
            .GreaterThanOrEqualTo(0);
    }
}
