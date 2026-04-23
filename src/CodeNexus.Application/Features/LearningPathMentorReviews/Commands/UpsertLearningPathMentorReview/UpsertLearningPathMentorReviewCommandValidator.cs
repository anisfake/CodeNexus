using FluentValidation;

namespace CodeNexus.Application.Features.LearningPathMentorReviews.Commands.UpsertLearningPathMentorReview;

public class UpsertLearningPathMentorReviewCommandValidator : AbstractValidator<UpsertLearningPathMentorReviewCommand>
{
    public UpsertLearningPathMentorReviewCommandValidator()
    {
        RuleFor(x => x.PathId)
            .NotEmpty()
            .WithMessage("PathId is required.");

        RuleFor(x => x.ChangeSummary)
            .NotEmpty()
            .WithMessage("ChangeSummary is required.")
            .MaximumLength(2000)
            .WithMessage("ChangeSummary must not exceed 2000 characters.");

        RuleFor(x => x.ChangeReason)
            .NotEmpty()
            .WithMessage("ChangeReason is required.")
            .MaximumLength(2000)
            .WithMessage("ChangeReason must not exceed 2000 characters.");
    }
}
