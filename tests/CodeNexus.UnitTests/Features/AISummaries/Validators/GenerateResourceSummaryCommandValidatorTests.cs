using CodeNexus.Application.Features.AISummaries.Commands.GenerateResourceSummary;
using FluentValidation.TestHelper;
using MassTransit;
using Xunit;

namespace CodeNexus.UnitTests.Features.AISummaries.Validators;

public class GenerateResourceSummaryCommandValidatorTests
{
    private readonly GenerateResourceSummaryCommandValidator _validator;

    public GenerateResourceSummaryCommandValidatorTests()
    {
        _validator = new GenerateResourceSummaryCommandValidator();
    }

    [Fact]
    public void Validate_ValidCommand_ShouldNotHaveErrors()
    {
        // Arrange
        var command = new GenerateResourceSummaryCommand(NewId.NextGuid(), 1, 5);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyResourceId_ShouldHaveError()
    {
        // Arrange
        var command = new GenerateResourceSummaryCommand(Guid.Empty, 1, 3);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ResourceId)
            .WithErrorCode("RESOURCE_ID_REQUIRED");
    }

    [Fact]
    public void Validate_StartPageZero_ShouldHaveError()
    {
        // Arrange
        var command = new GenerateResourceSummaryCommand(NewId.NextGuid(), 0, 3);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.StartPage)
            .WithErrorCode("INVALID_START_PAGE");
    }

    [Fact]
    public void Validate_NegativeStartPage_ShouldHaveError()
    {
        // Arrange
        var command = new GenerateResourceSummaryCommand(NewId.NextGuid(), -1, 3);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.StartPage)
            .WithErrorCode("INVALID_START_PAGE");
    }

    [Fact]
    public void Validate_EndPageLessThanStartPage_ShouldHaveError()
    {
        // Arrange
        var command = new GenerateResourceSummaryCommand(NewId.NextGuid(), 5, 3);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.EndPage)
            .WithErrorCode("INVALID_END_PAGE");
    }

    [Fact]
    public void Validate_MoreThan5Pages_ShouldHaveError()
    {
        // Arrange
        var command = new GenerateResourceSummaryCommand(NewId.NextGuid(), 1, 7);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x)
            .WithErrorCode("MAX_PAGES_EXCEEDED");
    }

    [Fact]
    public void Validate_Exactly5Pages_ShouldNotHaveError()
    {
        // Arrange
        var command = new GenerateResourceSummaryCommand(NewId.NextGuid(), 3, 7);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_SinglePage_ShouldNotHaveError()
    {
        // Arrange
        var command = new GenerateResourceSummaryCommand(NewId.NextGuid(), 5, 5);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_6Pages_ShouldHaveError()
    {
        // Arrange
        var command = new GenerateResourceSummaryCommand(NewId.NextGuid(), 1, 6);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x)
            .WithErrorCode("MAX_PAGES_EXCEEDED");
    }
}
