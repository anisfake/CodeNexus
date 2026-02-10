using CodeNexus.Application.Features.LearningPaths.Commands.GenerateLearningPathSkeleton;
using FluentValidation.TestHelper;
using Xunit;

namespace CodeNexus.UnitTests.Features.LearningPaths;

public class GenerateLearningPathSkeletonCommandValidatorTests
{
    private readonly GenerateLearningPathSkeletonCommandValidator _validator;

    public GenerateLearningPathSkeletonCommandValidatorTests()
    {
        _validator = new GenerateLearningPathSkeletonCommandValidator();
    }

    [Fact]
    public void Validate_WithValidCommand_ShouldNotHaveErrors()
    {
        // Arrange
        var command = new GenerateLearningPathSkeletonCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptySubjectId_ShouldHaveError()
    {
        // Arrange
        var command = new GenerateLearningPathSkeletonCommand(Guid.Empty, Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.SubjectId);
    }

    [Fact]
    public void Validate_WithEmptyGoalId_ShouldHaveError()
    {
        // Arrange
        var command = new GenerateLearningPathSkeletonCommand(Guid.NewGuid(), Guid.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.GoalId);
    }

    [Fact]
    public void Validate_WithBothEmptyIds_ShouldHaveErrors()
    {
        // Arrange
        var command = new GenerateLearningPathSkeletonCommand(Guid.Empty, Guid.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.SubjectId);
        result.ShouldHaveValidationErrorFor(x => x.GoalId);
    }
}
