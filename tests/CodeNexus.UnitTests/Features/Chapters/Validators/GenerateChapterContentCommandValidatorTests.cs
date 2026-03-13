using CodeNexus.Application.Features.Chapters.Commands.GenerateChapterContent;
using FluentValidation.TestHelper;
using MassTransit;
using Xunit;

namespace CodeNexus.UnitTests.Features.Chapters.Validators;

public class GenerateChapterContentCommandValidatorTests
{
    private readonly GenerateChapterContentCommandValidator _validator;

    public GenerateChapterContentCommandValidatorTests()
    {
        _validator = new GenerateChapterContentCommandValidator();
    }

    [Fact]
    public void Validate_ValidChapterId_ShouldNotHaveError()
    {
        // Arrange
        var command = new GenerateChapterContentCommand(NewId.NextGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.ChapterId);
    }

    [Fact]
    public void Validate_EmptyChapterId_ShouldHaveError()
    {
        // Arrange
        var command = new GenerateChapterContentCommand(Guid.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ChapterId)
            .WithErrorCode("CHAPTER_ID_REQUIRED");
    }
}
