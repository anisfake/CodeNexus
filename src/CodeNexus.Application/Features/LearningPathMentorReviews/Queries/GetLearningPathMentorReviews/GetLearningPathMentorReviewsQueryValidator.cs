using FluentValidation;

namespace CodeNexus.Application.Features.LearningPathMentorReviews.Queries.GetLearningPathMentorReviews;

public class GetLearningPathMentorReviewsQueryValidator : AbstractValidator<GetLearningPathMentorReviewsQuery>
{
    public GetLearningPathMentorReviewsQueryValidator()
    {
        RuleFor(x => x.PathId)
            .NotEmpty()
            .WithMessage("PathId is required.");
    }
}

