using CodeNexus.Application.Features.Goals.Commands.CreateGoal;
using CodeNexus.Domain.Enums;
using FluentValidation.TestHelper;
using Xunit;

namespace CodeNexus.UnitTests.Features.Goals;

public class CreateGoalCommandValidatorTests
{
    private readonly CreateGoalCommandValidator _validator;

    public CreateGoalCommandValidatorTests()
    {
        _validator = new CreateGoalCommandValidator();
    }

    [Fact]
    public void Validate_WithValidCommand_ShouldNotHaveErrors()
    {
        // Arrange
        var command = new CreateGoalCommand("Learn C# Programming", "Master C# programming", GoalDuration.OneMonth);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyTitle_ShouldHaveError()
    {
        // Arrange
        var command = new CreateGoalCommand(string.Empty, "Description", GoalDuration.OneMonth);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Validate_WithTitleExceedingMaxLength_ShouldHaveError()
    {
        // Arrange
        var longTitle = new string('a', 201);
        var command = new CreateGoalCommand(longTitle, "Description", GoalDuration.OneMonth);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Validate_WithDescriptionExceedingMaxLength_ShouldHaveError()
    {
        // Arrange
        var longDescription = new string('a', 501);
        var command = new CreateGoalCommand("Learn C# Programming", longDescription, GoalDuration.OneMonth);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_WithNullDescription_ShouldNotHaveError()
    {
        // Arrange
        var command = new CreateGoalCommand("Learn C# Programming", null, GoalDuration.OneMonth);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_WithMaxLengthTitle_ShouldNotHaveError()
    {
        // Arrange
        var maxTitle = new string('a', 200);
        var command = new CreateGoalCommand(maxTitle, "Description", GoalDuration.OneMonth);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Validate_WithMaxLengthDescription_ShouldNotHaveError()
    {
        // Arrange
        var maxDescription = new string('a', 500);
        var command = new CreateGoalCommand("Learn C# Programming", maxDescription, GoalDuration.OneMonth);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_WithTitleTooShort_ShouldHaveError()
    {
        // Arrange
        var command = new CreateGoalCommand("Short", "Description", GoalDuration.OneMonth);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }
}
