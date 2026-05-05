using FluentValidation;

namespace CodeNexus.Application.Features.Mentors.Queries.GetMyMentorReviews;

public class GetMyMentorReviewsQueryValidator : AbstractValidator<GetMyMentorReviewsQuery>
{
    public GetMyMentorReviewsQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThan(0)
            .WithErrorCode("INVALID_PAGE_NUMBER")
            .WithMessage("PageNumber must be greater than 0.");

        RuleFor(x => x.PageSize)
            .GreaterThan(0)
            .LessThanOrEqualTo(100)
            .WithErrorCode("INVALID_PAGE_SIZE")
            .WithMessage("PageSize must be between 1 and 100.");
    }
}
