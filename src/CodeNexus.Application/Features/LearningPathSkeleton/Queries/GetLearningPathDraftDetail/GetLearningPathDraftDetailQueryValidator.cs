using FluentValidation;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Queries.GetLearningPathDraftDetail;

public class GetLearningPathDraftDetailQueryValidator : AbstractValidator<GetLearningPathDraftDetailQuery>
{
    public GetLearningPathDraftDetailQueryValidator()
    {
        RuleFor(x => x.PathId)
            .NotEmpty()
            .WithMessage("PathId is required.");
    }
}
