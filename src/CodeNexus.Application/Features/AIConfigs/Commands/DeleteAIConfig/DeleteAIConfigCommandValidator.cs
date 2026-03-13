using FluentValidation;

namespace CodeNexus.Application.Features.AIConfigs.Commands.DeleteAIConfig;

public class DeleteAIConfigCommandValidator : AbstractValidator<DeleteAIConfigCommand>
{
    public DeleteAIConfigCommandValidator()
    {
        RuleFor(x => x.ConfigId)
            .NotEmpty().WithMessage("ConfigId is required.");
    }
}
