using CodeNexus.Application.Features.Quizzes.Commands.GenerateSingleQuizSkeleton;
using FluentValidation.TestHelper;
using MassTransit;
using Xunit;

namespace CodeNexus.UnitTests.Features.Quizzes.Validators;

public class GenerateSingleQuizSkeletonCommandValidatorTests
{
    private readonly GenerateSingleQuizSkeletonCommandValidator _validator;

    public GenerateSingleQuizSkeletonCommandValidatorTests()
    {
        _validator = new GenerateSingleQuizSkeletonCommandValidator();
    }

    [Fact]
    public void Validate_ValidLessonId_ShouldNotHaveValidationError()
    {
        // Arrange
        var command = new GenerateSingleQuizSkeletonCommand(NewId.NextGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.LessonId);
    }

    [Fact]
    public void Validate_EmptyLessonId_ShouldHaveValidationError()
    {
        // Arrange
        var command = new GenerateSingleQuizSkeletonCommand(Guid.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.LessonId)
            .WithErrorCode("LESSON_ID_REQUIRED");
    }
}
