using FluentValidation;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Commands.AdoptSuggestedLearningPath;

public class AdoptSuggestedLearningPathCommandValidator : AbstractValidator<AdoptSuggestedLearningPathCommand>
{
    public AdoptSuggestedLearningPathCommandValidator()
    {
        RuleFor(x => x.SuggestedPathId)
            .NotEmpty()
            .WithMessage("SuggestedPathId is required.");

        RuleFor(x => x.SubjectId)
            .NotEmpty()
            .WithMessage("SubjectId is required.");

        RuleFor(x => x.Goals)
            .NotNull()
            .Must(g => g != null && g.Count > 0)
            .WithMessage("At least one goal is required.")
            .Must(g => g != null && g.Count <= 2)
            .WithMessage("You can select up to 2 goals only.")
            .Must(g => g != null && g.Sum(goal => goal.Weight) > 0)
            .WithMessage("Goal weights must be greater than 0.");
    }
}
