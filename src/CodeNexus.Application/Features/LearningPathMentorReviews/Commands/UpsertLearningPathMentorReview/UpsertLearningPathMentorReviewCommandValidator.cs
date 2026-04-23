using FluentValidation;

namespace CodeNexus.Application.Features.LearningPathMentorReviews.Commands.UpsertLearningPathMentorReview;

public class UpsertLearningPathMentorReviewCommandValidator : AbstractValidator<UpsertLearningPathMentorReviewCommand>
{
    public UpsertLearningPathMentorReviewCommandValidator()
    {
        RuleFor(x => x.PathId)
            .NotEmpty()
            .WithMessage("PathId is required.");

        RuleFor(x => x.Score)
            .InclusiveBetween(1, 5)
            .WithMessage("Score must be between 1 and 5.");

        RuleFor(x => x.Feedback)
            .NotEmpty()
            .WithMessage("Feedback is required.")
            .MaximumLength(2000)
            .WithMessage("Feedback must not exceed 2000 characters.");

        RuleFor(x => x.Suggestions)
            .MaximumLength(2000)
            .When(x => !string.IsNullOrWhiteSpace(x.Suggestions))
            .WithMessage("Suggestions must not exceed 2000 characters.");
    }
}

