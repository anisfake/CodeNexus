using FluentValidation;

namespace CodeNexus.Application.Features.LearningPathMentorReviews.Commands.RequestLearningPathMentorReview;

public class RequestLearningPathMentorReviewCommandValidator : AbstractValidator<RequestLearningPathMentorReviewCommand>
{
    public RequestLearningPathMentorReviewCommandValidator()
    {
        RuleFor(x => x.PathId)
            .NotEmpty()
            .WithMessage("PathId is required.");

        RuleFor(x => x.MentorId)
            .NotEmpty()
            .WithMessage("MentorId is required.");

        RuleFor(x => x.StudentRequestNote)
            .MaximumLength(1000)
            .When(x => !string.IsNullOrWhiteSpace(x.StudentRequestNote))
            .WithMessage("StudentRequestNote must not exceed 1000 characters.");
    }
}
