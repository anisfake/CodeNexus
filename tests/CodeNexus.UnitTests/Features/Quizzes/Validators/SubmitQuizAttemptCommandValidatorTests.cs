using CodeNexus.Application.Features.Quizzes.Commands.SubmitQuizAttempt;
using CodeNexus.Application.Features.Quizzes.DTOs;
using FluentValidation.TestHelper;
using MassTransit;
using Xunit;

namespace CodeNexus.UnitTests.Features.Quizzes.Validators;

public class SubmitQuizAttemptCommandValidatorTests
{
    private readonly SubmitQuizAttemptCommandValidator _validator;

    public SubmitQuizAttemptCommandValidatorTests()
    {
        _validator = new SubmitQuizAttemptCommandValidator();
    }

    [Fact]
    public void Validate_EmptyAttemptId_ShouldHaveError()
    {
        var command = new SubmitQuizAttemptCommand(Guid.Empty, new List<AnswerItemDto>());
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.AttemptId)
            .WithErrorCode("ATTEMPT_ID_REQUIRED");
    }

    [Fact]
    public void Validate_NullAnswers_ShouldHaveError()
    {
        var command = new SubmitQuizAttemptCommand(NewId.NextGuid(), null!);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Answers)
            .WithErrorCode("ANSWERS_REQUIRED");
    }

    [Fact]
    public void Validate_ValidCommand_ShouldNotHaveError()
    {
        var command = new SubmitQuizAttemptCommand(NewId.NextGuid(), new List<AnswerItemDto>
        {
            new(NewId.NextGuid(), "True")
        });
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
