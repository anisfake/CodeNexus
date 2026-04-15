using CodeNexus.Application.Features.AISummaries.Commands.DeleteResourceSummary;
using FluentValidation.TestHelper;
using MassTransit;
using Xunit;

namespace CodeNexus.UnitTests.Features.AISummaries.Validators;

public class DeleteResourceSummaryCommandValidatorTests
{
    private readonly DeleteResourceSummaryCommandValidator _validator;

    public DeleteResourceSummaryCommandValidatorTests()
    {
        _validator = new DeleteResourceSummaryCommandValidator();
    }

    [Fact]
    public void Validate_ValidCommand_ShouldNotHaveErrors()
    {
        // Arrange
        var command = new DeleteResourceSummaryCommand(NewId.NextGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptySummaryId_ShouldHaveError()
    {
        // Arrange
        var command = new DeleteResourceSummaryCommand(Guid.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.SummaryId)
            .WithErrorCode("SUMMARY_ID_REQUIRED");
    }
}
