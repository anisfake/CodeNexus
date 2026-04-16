using CodeNexus.Application.Features.Quizzes.Commands.GenerateSingleQuizQuestion;
using CodeNexus.Domain.Enums;
using FluentValidation.TestHelper;
using MassTransit;
using Xunit;

namespace CodeNexus.UnitTests.Features.Quizzes.Validators;

public class GenerateSingleQuizQuestionCommandValidatorTests
{
    private readonly GenerateSingleQuizQuestionCommandValidator _validator;

    public GenerateSingleQuizQuestionCommandValidatorTests()
    {
        _validator = new GenerateSingleQuizQuestionCommandValidator();
    }

    [Fact]
    public void Validate_ValidQuizId_ShouldNotHaveValidationError()
    {
        var command = new GenerateSingleQuizQuestionCommand(NewId.NextGuid(), QuestionType.SingleChoice);

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.QuizId);
    }

    [Fact]
    public void Validate_EmptyQuizId_ShouldHaveValidationError()
    {
        var command = new GenerateSingleQuizQuestionCommand(Guid.Empty, QuestionType.SingleChoice);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.QuizId)
            .WithErrorCode("QUIZ_ID_REQUIRED");
    }

    [Fact]
    public void Validate_InvalidQuestionType_ShouldHaveValidationError()
    {
        var command = new GenerateSingleQuizQuestionCommand(NewId.NextGuid(), (QuestionType)99);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.QuestionType)
            .WithErrorCode("QUESTION_TYPE_INVALID");
    }
}
