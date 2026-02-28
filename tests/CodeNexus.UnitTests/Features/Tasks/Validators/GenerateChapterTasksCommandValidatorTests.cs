using CodeNexus.Application.Features.Tasks.Commands.GenerateChapterTasks;
using FluentValidation.TestHelper;
using MassTransit;
using Xunit;

namespace CodeNexus.UnitTests.Features.Tasks.Validators;

public class GenerateChapterTasksCommandValidatorTests
{
    private readonly GenerateChapterTasksCommandValidator _validator;

    public GenerateChapterTasksCommandValidatorTests()
    {
        _validator = new GenerateChapterTasksCommandValidator();
    }

    [Fact]
    public void Validate_ValidChapterId_ShouldNotHaveError()
    {
        // Arrange
        var command = new GenerateChapterTasksCommand(NewId.NextGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.ChapterId);
    }

    [Fact]
    public void Validate_EmptyChapterId_ShouldHaveError()
    {
        // Arrange
        var command = new GenerateChapterTasksCommand(Guid.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ChapterId)
            .WithErrorCode("CHAPTER_ID_REQUIRED");
    }
}
