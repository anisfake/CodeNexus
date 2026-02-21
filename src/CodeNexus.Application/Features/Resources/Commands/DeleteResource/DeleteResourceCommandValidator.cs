using FluentValidation;

namespace CodeNexus.Application.Features.Resources.Commands.DeleteResource;

public class DeleteResourceCommandValidator : AbstractValidator<DeleteResourceCommand>
{
    public DeleteResourceCommandValidator()
    {
        RuleFor(x => x.ResourceId)
            .NotEmpty().WithMessage("ResourceId is required.");
    }
}
