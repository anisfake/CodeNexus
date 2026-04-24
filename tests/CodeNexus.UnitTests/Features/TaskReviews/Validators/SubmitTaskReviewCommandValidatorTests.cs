using CodeNexus.Application.Features.TaskReviews.Commands.SubmitTaskReview;
using FluentValidation.TestHelper;
using MassTransit;

namespace CodeNexus.UnitTests.Features.TaskReviews.Validators;

public class SubmitTaskReviewCommandValidatorTests
{
    private readonly SubmitTaskReviewCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_NoErrors()
    {
        var command = new SubmitTaskReviewCommand(NewId.NextGuid(), 85, "Great work!", "Consider using async methods.");
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ScoreBelowZero_HasError()
    {
        var command = new SubmitTaskReviewCommand(NewId.NextGuid(), -1, "OK", null);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Score)
              .WithErrorCode("INVALID_SCORE");
    }

    [Fact]
    public void Validate_ScoreAbove100_HasError()
    {
        var command = new SubmitTaskReviewCommand(NewId.NextGuid(), 101, "OK", null);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Score)
              .WithErrorCode("INVALID_SCORE");
    }

    [Fact]
    public void Validate_BoundaryScores_NoErrors()
    {
        var commandZero = new SubmitTaskReviewCommand(NewId.NextGuid(), 0, "Needs improvement", null);
        var commandHundred = new SubmitTaskReviewCommand(NewId.NextGuid(), 100, "Perfect!", null);

        _validator.TestValidate(commandZero).ShouldNotHaveAnyValidationErrors();
        _validator.TestValidate(commandHundred).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyFeedback_HasError()
    {
        var command = new SubmitTaskReviewCommand(NewId.NextGuid(), 75, "", null);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Feedback)
              .WithErrorCode("FEEDBACK_REQUIRED");
    }

    [Fact]
    public void Validate_FeedbackTooLong_HasError()
    {
        var longFeedback = new string('x', 2001);
        var command = new SubmitTaskReviewCommand(NewId.NextGuid(), 75, longFeedback, null);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Feedback)
              .WithErrorCode("FEEDBACK_TOO_LONG");
    }

    [Fact]
    public void Validate_SuggestionsTooLong_HasError()
    {
        var longSuggestions = new string('s', 2001);
        var command = new SubmitTaskReviewCommand(NewId.NextGuid(), 75, "OK", longSuggestions);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Suggestions)
              .WithErrorCode("SUGGESTIONS_TOO_LONG");
    }

    [Fact]
    public void Validate_NullSuggestions_NoErrors()
    {
        var command = new SubmitTaskReviewCommand(NewId.NextGuid(), 75, "Good work", null);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyReviewId_HasError()
    {
        var command = new SubmitTaskReviewCommand(Guid.Empty, 75, "Good", null);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ReviewId)
              .WithErrorCode("REVIEW_ID_REQUIRED");
    }
}
