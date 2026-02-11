using CodeNexus.Application.Features.Lessons.Commands.GenerateLessonContent;
using FluentValidation.TestHelper;
using Xunit;

namespace CodeNexus.UnitTests.Features.Lessons;

public class GenerateLessonContentCommandValidatorTests
{
    private readonly GenerateLessonContentCommandValidator _validator;

    public GenerateLessonContentCommandValidatorTests()
    {
        _validator = new GenerateLessonContentCommandValidator();
    }

    [Fact]
    public void Validate_ValidLessonId_ShouldNotHaveErrors()
    {
        // Arrange
        var command = new GenerateLessonContentCommand(Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyLessonId_ShouldHaveError()
    {
        // Arrange
        var command = new GenerateLessonContentCommand(Guid.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.LessonId)
            .WithErrorCode("LESSON_ID_REQUIRED");
    }
}
