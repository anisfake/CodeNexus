using FluentValidation;

namespace CodeNexus.Application.Features.AIConfigs.Commands.UpdateAIConfig;

public class UpdateAIConfigCommandValidator : AbstractValidator<UpdateAIConfigCommand>
{
    public UpdateAIConfigCommandValidator()
    {
        RuleFor(x => x.ProviderName)
            .NotEmpty().WithMessage("Provider name is required.")
            .MaximumLength(100).WithMessage("Provider name must not exceed 100 characters.");

        When(x => x.ApiKey != null, () =>
        {
            RuleFor(x => x.ApiKey)
                .NotEmpty().WithMessage("API key cannot be empty if provided.");
        });
    }
}
