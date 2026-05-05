using FluentValidation;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Queries.GetLearningPathSuggestionPreview;

public class GetLearningPathSuggestionPreviewQueryValidator : AbstractValidator<GetLearningPathSuggestionPreviewQuery>
{
    public GetLearningPathSuggestionPreviewQueryValidator()
    {
        RuleFor(x => x.SuggestedPathId)
            .NotEmpty()
            .WithErrorCode("PATH_ID_REQUIRED")
            .WithMessage("SuggestedPathId is required.");

        RuleFor(x => x.SubjectId)
            .NotEmpty()
            .WithErrorCode("SUBJECT_ID_REQUIRED")
            .WithMessage("SubjectId is required.");

        RuleFor(x => x.Goals)
            .NotNull()
            .WithErrorCode("GOALS_REQUIRED")
            .WithMessage("At least one goal is required.")
            .Must(g => g != null && g.Count > 0)
            .WithErrorCode("GOALS_REQUIRED")
            .WithMessage("At least one goal is required.")
            .Must(g => g != null && g.Count <= 2)
            .WithErrorCode("GOALS_LIMIT_EXCEEDED")
            .WithMessage("You can select up to 2 goals only.");
    }
}
