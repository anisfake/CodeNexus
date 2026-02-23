using FluentValidation;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Queries.GetLearningPathByUserId;

public class GetLearningPathByUserIdQueryValidator : AbstractValidator<GetLearningPathByUserIdQuery>
{
    public GetLearningPathByUserIdQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");

        RuleFor(x => x.PageNumber)
            .GreaterThan(0).WithMessage("PageNumber must be greater than 0.");

        RuleFor(x => x.PageSize)
            .GreaterThan(0).WithMessage("PageSize must be greater than 0.")
            .LessThanOrEqualTo(100).WithMessage("PageSize must not exceed 100.");
    }
}
