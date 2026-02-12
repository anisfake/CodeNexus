using CodeNexus.Application.Features.Goals.Commands.CreateGoal;
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
        var command = new CreateGoalCommand("Learn C#", "Master C# programming", 60);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyTitle_ShouldHaveError()
    {
        // Arrange
        var command = new CreateGoalCommand(string.Empty, "Description", 30);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Validate_WithTitleExceedingMaxLength_ShouldHaveError()
    {
        // Arrange
        var longTitle = new string('a', 101);
        var command = new CreateGoalCommand(longTitle, "Description", 30);

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
        var command = new CreateGoalCommand("Learn C#", longDescription, 30);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_WithZeroDurationDays_ShouldHaveError()
    {
        // Arrange
        var command = new CreateGoalCommand("Learn C#", "Description", 0);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.DurationsDay);
    }

    [Fact]
    public void Validate_WithNegativeDurationDays_ShouldHaveError()
    {
        // Arrange
        var command = new CreateGoalCommand("Learn C#", "Description", -10);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.DurationsDay);
    }

    [Fact]
    public void Validate_WithNullDescription_ShouldNotHaveError()
    {
        // Arrange
        var command = new CreateGoalCommand("Learn C#", null, 30);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_WithMaxLengthTitle_ShouldNotHaveError()
    {
        // Arrange
        var maxTitle = new string('a', 100);
        var command = new CreateGoalCommand(maxTitle, "Description", 30);

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
        var command = new CreateGoalCommand("Learn C#", maxDescription, 30);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_WithLargeDurationDays_ShouldNotHaveError()
    {
        // Arrange
        var command = new CreateGoalCommand("Learn C#", "Description", 365);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.DurationsDay);
    }
}
