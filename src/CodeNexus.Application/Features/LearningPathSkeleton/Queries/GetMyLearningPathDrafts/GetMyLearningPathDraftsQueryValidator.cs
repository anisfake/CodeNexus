using FluentValidation;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Queries.GetMyLearningPathDrafts;

public class GetMyLearningPathDraftsQueryValidator : AbstractValidator<GetMyLearningPathDraftsQuery>
{
    public GetMyLearningPathDraftsQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThan(0).WithMessage("PageNumber must be greater than 0.");

        RuleFor(x => x.PageSize)
            .GreaterThan(0).WithMessage("PageSize must be greater than 0.")
            .LessThanOrEqualTo(100).WithMessage("PageSize must not exceed 100.");
    }
}
