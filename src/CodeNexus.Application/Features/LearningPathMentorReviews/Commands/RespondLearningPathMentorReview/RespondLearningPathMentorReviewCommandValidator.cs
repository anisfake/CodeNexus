using CodeNexus.Domain.Enums;
using FluentValidation;

namespace CodeNexus.Application.Features.LearningPathMentorReviews.Commands.RespondLearningPathMentorReview;

public class RespondLearningPathMentorReviewCommandValidator : AbstractValidator<RespondLearningPathMentorReviewCommand>
{
    public RespondLearningPathMentorReviewCommandValidator()
    {
        RuleFor(x => x.PathId)
            .NotEmpty()
            .WithMessage("PathId is required.");

        RuleFor(x => x.ReviewId)
            .NotEmpty()
            .WithMessage("ReviewId is required.");

        RuleFor(x => x.DecisionStatus)
            .Must(x => x is LearningPathMentorReviewDecisionStatus.Accepted or LearningPathMentorReviewDecisionStatus.Rejected)
            .WithMessage("DecisionStatus must be Accepted or Rejected.");

        RuleFor(x => x.StudentDecisionNote)
            .MaximumLength(1000)
            .When(x => !string.IsNullOrWhiteSpace(x.StudentDecisionNote))
            .WithMessage("StudentDecisionNote must not exceed 1000 characters.");
    }
}
