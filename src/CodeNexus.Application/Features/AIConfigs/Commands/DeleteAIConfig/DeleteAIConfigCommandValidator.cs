using FluentValidation;

namespace CodeNexus.Application.Features.AIConfigs.Commands.DeleteAIConfig;

public class DeleteAIConfigCommandValidator : AbstractValidator<DeleteAIConfigCommand>
{
    public DeleteAIConfigCommandValidator()
    {
        RuleFor(x => x.ProviderName)
            .NotEmpty().WithMessage("Provider name is required.")
            .MaximumLength(100).WithMessage("Provider name must not exceed 100 characters.");
    }
}
