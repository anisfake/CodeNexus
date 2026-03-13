using FluentValidation;

namespace CodeNexus.Application.Features.AIConfigs.Commands.CreateAIConfig
{
    public class CreateAIConfigCommandValidator : AbstractValidator<CreateAIConfigCommand>
    {
        public CreateAIConfigCommandValidator()
        {
            RuleFor(x => x.ProviderName)
                .NotEmpty().WithMessage("Provider name is required")
                .MaximumLength(50).WithMessage("Provider name must not exceed 50 characters");

            RuleFor(x => x.ApiKey)
                .NotEmpty().WithMessage("API key is required")
                .MinimumLength(10).WithMessage("API key must be at least 10 characters");

            RuleFor(x => x.ConfigJson)
                .NotNull().WithMessage("Config JSON is required")
                .Must(x => x.Count > 0).WithMessage("Config JSON must contain at least one field");

            RuleFor(x => x.AIUsageType)
                .IsInEnum().WithMessage("Invalid AI usage type");
        }
    }
}
