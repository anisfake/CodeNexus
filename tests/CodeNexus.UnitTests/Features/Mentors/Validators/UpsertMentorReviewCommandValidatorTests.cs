using CodeNexus.Application.Features.Mentors.Commands.UpsertMentorReview;
using FluentValidation.TestHelper;
using MassTransit;

namespace CodeNexus.UnitTests.Features.Mentors.Validators;

public class UpsertMentorReviewCommandValidatorTests
{
    private readonly UpsertMentorReviewCommandValidator _validator = new();

    [Fact]
    public void Validate_EmptyMentorId_ShouldHaveValidationError()
    {
        var command = new UpsertMentorReviewCommand(Guid.Empty, 5, "Good");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.MentorId)
            .WithErrorCode("MENTOR_ID_REQUIRED");
    }

    [Fact]
    public void Validate_InvalidScore_ShouldHaveValidationError()
    {
        var command = new UpsertMentorReviewCommand(NewId.NextGuid(), 0, "Good");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Score)
            .WithErrorCode("INVALID_RATING_SCORE");
    }

    [Fact]
    public void Validate_ValidCommand_ShouldNotHaveValidationError()
    {
        var command = new UpsertMentorReviewCommand(NewId.NextGuid(), 4, "Helpful mentor");

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
