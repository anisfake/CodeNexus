using CodeNexus.Application.Features.TaskReviews.Commands.RequestTaskReview;
using FluentValidation.TestHelper;
using MassTransit;

namespace CodeNexus.UnitTests.Features.TaskReviews.Validators;

public class RequestTaskReviewCommandValidatorTests
{
    private readonly RequestTaskReviewCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_NoErrors()
    {
        var command = new RequestTaskReviewCommand(NewId.NextGuid(), NewId.NextGuid(), "Please review");
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptySessionId_HasError()
    {
        var command = new RequestTaskReviewCommand(Guid.Empty, NewId.NextGuid(), null);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.SessionId)
              .WithErrorCode("SESSION_ID_REQUIRED");
    }

    [Fact]
    public void Validate_EmptyMentorId_HasError()
    {
        var command = new RequestTaskReviewCommand(NewId.NextGuid(), Guid.Empty, null);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.MentorId)
              .WithErrorCode("MENTOR_ID_REQUIRED");
    }

    [Fact]
    public void Validate_NullRequestNote_NoErrors()
    {
        var command = new RequestTaskReviewCommand(NewId.NextGuid(), NewId.NextGuid(), null);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_RequestNoteTooLong_HasError()
    {
        var longNote = new string('a', 501);
        var command = new RequestTaskReviewCommand(NewId.NextGuid(), NewId.NextGuid(), longNote);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.StudentRequestNote)
              .WithErrorCode("REQUEST_NOTE_TOO_LONG");
    }
}
