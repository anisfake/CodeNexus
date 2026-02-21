using FluentValidation;

namespace CodeNexus.Application.Features.Resources.Commands.UpdateResource;

public class UpdateResourceCommandValidator : AbstractValidator<UpdateResourceCommand>
{
    public UpdateResourceCommandValidator()
    {
        RuleFor(x => x.ResourceId)
            .NotEmpty().WithMessage("ResourceId is required.");

        RuleFor(x => x.Title)
            .MaximumLength(100).WithMessage("Title must not exceed 100 characters.")
            .When(x => !string.IsNullOrEmpty(x.Title));

        RuleFor(x => x.Url)
            .MaximumLength(500).WithMessage("URL must not exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.Url));

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must not exceed 1000 characters.")
            .When(x => !string.IsNullOrEmpty(x.Description));
    }
}
