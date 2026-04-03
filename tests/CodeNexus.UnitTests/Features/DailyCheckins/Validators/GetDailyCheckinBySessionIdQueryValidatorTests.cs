using CodeNexus.Application.Features.DailyCheckin.Queries.GetDailyCheckinBySessionId;
using FluentValidation.TestHelper;
using MassTransit;
using Xunit;

namespace CodeNexus.UnitTests.Features.DailyCheckinQueries;

public class GetDailyCheckinBySessionIdQueryValidatorTests
{
    private readonly GetDailyCheckinBySessionIdQueryValidator _validator;

    public GetDailyCheckinBySessionIdQueryValidatorTests()
    {
        _validator = new GetDailyCheckinBySessionIdQueryValidator();
    }

    [Fact]
    public void Validate_WithValidSessionId_ShouldNotHaveValidationErrors()
    {
        // Arrange
        var query = new GetDailyCheckinBySessionIdQuery(NewId.NextGuid());

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptySessionId_ShouldHaveValidationError()
    {
        // Arrange
        var query = new GetDailyCheckinBySessionIdQuery(Guid.Empty);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.SessionId)
            .WithErrorCode("SESSION_ID_REQUIRED");
    }
}
