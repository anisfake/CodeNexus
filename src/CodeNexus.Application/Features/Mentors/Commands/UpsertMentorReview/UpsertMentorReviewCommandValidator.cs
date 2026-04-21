using FluentValidation;

namespace CodeNexus.Application.Features.Mentors.Commands.UpsertMentorReview;

public class UpsertMentorReviewCommandValidator : AbstractValidator<UpsertMentorReviewCommand>
{
    public UpsertMentorReviewCommandValidator()
    {
        RuleFor(x => x.MentorId)
            .NotEmpty()
            .WithErrorCode("MENTOR_ID_REQUIRED")
            .WithMessage("MentorId is required.");

        RuleFor(x => x.Score)
            .InclusiveBetween(1, 5)
            .WithErrorCode("INVALID_RATING_SCORE")
            .WithMessage("Score must be between 1 and 5.");

        RuleFor(x => x.Comment)
            .MaximumLength(1000)
            .WithErrorCode("COMMENT_TOO_LONG")
            .WithMessage("Comment must be at most 1000 characters.");
    }
}
