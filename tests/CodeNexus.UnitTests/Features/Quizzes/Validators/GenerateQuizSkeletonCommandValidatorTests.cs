using CodeNexus.Application.Features.Quizzes.Commands.GenerateQuizSkeleton;
using FluentValidation.TestHelper;
using MassTransit;
using Xunit;

namespace CodeNexus.UnitTests.Features.Quizzes.Validators;

public class GenerateQuizSkeletonCommandValidatorTests
{
    private readonly GenerateQuizSkeletonCommandValidator _validator;

    public GenerateQuizSkeletonCommandValidatorTests()
    {
        _validator = new GenerateQuizSkeletonCommandValidator();
    }

    [Fact]
    public void Validate_ValidLessonId_ShouldNotHaveValidationError()
    {
        // Arrange
        var command = new GenerateQuizSkeletonCommand(NewId.NextGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.LessonId);
    }

    [Fact]
    public void Validate_EmptyLessonId_ShouldHaveValidationError()
    {
        // Arrange
        var command = new GenerateQuizSkeletonCommand(Guid.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.LessonId)
            .WithErrorMessage("LessonId is required");
    }
}