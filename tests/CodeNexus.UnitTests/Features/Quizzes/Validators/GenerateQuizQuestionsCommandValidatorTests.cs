using CodeNexus.Application.Features.Quizzes.Commands.GenerateQuizQuestions;
using FluentValidation.TestHelper;
using MassTransit;
using Xunit;

namespace CodeNexus.UnitTests.Features.Quizzes.Validators;

public class GenerateQuizQuestionsCommandValidatorTests
{
    private readonly GenerateQuizQuestionsCommandValidator _validator;

    public GenerateQuizQuestionsCommandValidatorTests()
    {
        _validator = new GenerateQuizQuestionsCommandValidator();
    }

    [Fact]
    public void Validate_ValidQuizId_ShouldNotHaveError()
    {
        // Arrange
        var command = new GenerateQuizQuestionsCommand(NewId.NextGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.QuizId);
    }

    [Fact]
    public void Validate_EmptyQuizId_ShouldHaveError()
    {
        // Arrange
        var command = new GenerateQuizQuestionsCommand(Guid.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.QuizId)
            .WithErrorCode("QUIZ_ID_REQUIRED");
    }
}
