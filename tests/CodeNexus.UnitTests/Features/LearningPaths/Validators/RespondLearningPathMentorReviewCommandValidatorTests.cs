using CodeNexus.Application.Features.LearningPathMentorReviews.Commands.RespondLearningPathMentorReview;
using CodeNexus.Domain.Enums;
using FluentValidation.TestHelper;

namespace CodeNexus.UnitTests.Features.LearningPaths.Validators;

public class RespondLearningPathMentorReviewCommandValidatorTests
{
    private readonly RespondLearningPathMentorReviewCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenDecisionStatusPending_ShouldHaveValidationError()
    {
        var command = new RespondLearningPathMentorReviewCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            LearningPathMentorReviewDecisionStatus.Pending,
            null);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.DecisionStatus);
    }

    [Fact]
    public void Validate_WhenDecisionStatusAccepted_ShouldNotHaveValidationError()
    {
        var command = new RespondLearningPathMentorReviewCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            LearningPathMentorReviewDecisionStatus.Accepted,
            "OK de nghi nay hop ly");

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
