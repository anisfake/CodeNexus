using CodeNexus.Application.Features.Lessons.Commands.ConfirmLessonContent;
using FluentValidation.TestHelper;
using Xunit;

namespace CodeNexus.UnitTests.Features.Lessons.Validators;

public class ConfirmLessonContentCommandValidatorTests
{
    private readonly ConfirmLessonContentCommandValidator _validator;

    public ConfirmLessonContentCommandValidatorTests()
    {
        _validator = new ConfirmLessonContentCommandValidator();
    }

    [Fact]
    public void Validate_ValidCommand_ShouldNotHaveErrors()
    {
        // Arrange
        var command = new ConfirmLessonContentCommand(Guid.NewGuid(), "Some lesson content");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyLessonId_ShouldHaveError()
    {
        // Arrange
        var command = new ConfirmLessonContentCommand(Guid.Empty, "Some content");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.LessonId)
            .WithErrorCode("LESSON_ID_REQUIRED");
    }

    [Fact]
    public void Validate_EmptyContent_ShouldHaveError()
    {
        // Arrange
        var command = new ConfirmLessonContentCommand(Guid.NewGuid(), "");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Content)
            .WithErrorCode("CONTENT_REQUIRED");
    }
}
