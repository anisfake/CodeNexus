using CodeNexus.Application.Features.Quizzes.Commands.StartQuizAttempt;
using FluentValidation.TestHelper;
using Xunit;

namespace CodeNexus.UnitTests.Features.Quizzes.Validators;

public class StartQuizAttemptCommandValidatorTests
{
    private readonly StartQuizAttemptCommandValidator _validator;

    public StartQuizAttemptCommandValidatorTests()
    {
        _validator = new StartQuizAttemptCommandValidator();
    }

    [Fact]
    public void Validate_EmptyQuizId_ShouldHaveError()
    {
        var command = new StartQuizAttemptCommand(Guid.Empty);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.QuizId)
            .WithErrorCode("QUIZ_ID_REQUIRED");
    }

    [Fact]
    public void Validate_ValidQuizId_ShouldNotHaveError()
    {
        var command = new StartQuizAttemptCommand(Guid.NewGuid());
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.QuizId);
    }
}
