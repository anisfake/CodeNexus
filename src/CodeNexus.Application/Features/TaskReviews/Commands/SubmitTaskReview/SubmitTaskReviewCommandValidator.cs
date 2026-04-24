using FluentValidation;

namespace CodeNexus.Application.Features.TaskReviews.Commands.SubmitTaskReview;

public class SubmitTaskReviewCommandValidator : AbstractValidator<SubmitTaskReviewCommand>
{
    public SubmitTaskReviewCommandValidator()
    {
        RuleFor(x => x.ReviewId)
            .NotEmpty()
            .WithErrorCode("REVIEW_ID_REQUIRED")
            .WithMessage("Review ID is required.");

        RuleFor(x => x.Score)
            .InclusiveBetween(0, 100)
            .WithErrorCode("INVALID_SCORE")
            .WithMessage("Score must be between 0 and 100.");

        RuleFor(x => x.Feedback)
            .NotEmpty()
            .WithErrorCode("FEEDBACK_REQUIRED")
            .WithMessage("Feedback is required.")
            .MaximumLength(2000)
            .WithErrorCode("FEEDBACK_TOO_LONG")
            .WithMessage("Feedback must not exceed 2000 characters.");

        RuleFor(x => x.Suggestions)
            .MaximumLength(2000)
            .When(x => x.Suggestions != null)
            .WithErrorCode("SUGGESTIONS_TOO_LONG")
            .WithMessage("Suggestions must not exceed 2000 characters.");
    }
}
