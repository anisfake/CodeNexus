using CodeNexus.Application.Features.AISummaries.Queries.GetResourceSummaries;
using FluentValidation.TestHelper;
using MassTransit;
using Xunit;

namespace CodeNexus.UnitTests.Features.AISummaries.Validators;

public class GetResourceSummariesQueryValidatorTests
{
    private readonly GetResourceSummariesQueryValidator _validator;

    public GetResourceSummariesQueryValidatorTests()
    {
        _validator = new GetResourceSummariesQueryValidator();
    }

    [Fact]
    public void Validate_ValidQuery_ShouldNotHaveErrors()
    {
        // Arrange
        var query = new GetResourceSummariesQuery(NewId.NextGuid());

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyResourceId_ShouldHaveError()
    {
        // Arrange
        var query = new GetResourceSummariesQuery(Guid.Empty);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ResourceId)
            .WithErrorCode("RESOURCE_ID_REQUIRED");
    }
}
