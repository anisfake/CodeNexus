using FluentValidation;

namespace CodeNexus.Application.Features.TokenPackages.Commands.DeleteTokenPackage;

public class DeleteTokenPackageCommandValidator : AbstractValidator<DeleteTokenPackageCommand>
{
    public DeleteTokenPackageCommandValidator()
    {
        RuleFor(x => x.TokenPackageId).NotEmpty();
    }
}
