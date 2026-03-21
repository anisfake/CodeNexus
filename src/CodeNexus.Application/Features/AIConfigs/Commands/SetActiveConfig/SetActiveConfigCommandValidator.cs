using FluentValidation;

namespace CodeNexus.Application.Features.AIConfigs.Commands.SetActiveConfig;

public class SetActiveConfigCommandValidator : AbstractValidator<SetActiveConfigCommand>
{
    public SetActiveConfigCommandValidator()
    {
        RuleFor(x => x.ConfigId)
            .NotEmpty().WithMessage("ConfigId is required");

        RuleFor(x => x.UsageType)
            .IsInEnum().WithMessage("Invalid UsageType");

        RuleFor(x => x.AccessTier)
            .IsInEnum().WithMessage("Invalid AccessTier");
    }
}
