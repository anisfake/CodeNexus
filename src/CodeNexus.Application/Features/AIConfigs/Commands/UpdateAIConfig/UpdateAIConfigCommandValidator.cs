using FluentValidation;

namespace CodeNexus.Application.Features.AIConfigs.Commands.UpdateAIConfig;

public class UpdateAIConfigCommandValidator : AbstractValidator<UpdateAIConfigCommand>
{
    public UpdateAIConfigCommandValidator()
    {
        RuleFor(x => x.ConfigId)
            .NotEmpty().WithMessage("ConfigId is required.");

        When(x => x.ApiKey != null, () =>
        {
            RuleFor(x => x.ApiKey)
                .NotEmpty().WithMessage("API key cannot be empty if provided.");
        });

    }
}
