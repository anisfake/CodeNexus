using CodeNexus.Application.Features.DailyCheckin.Queries.GetMyDailyCheckins;
using FluentValidation.TestHelper;
using Xunit;

namespace CodeNexus.UnitTests.Features.DailyCheckinQueries;

public class GetMyDailyCheckinsQueryValidatorTests
{
    private readonly GetMyDailyCheckinsQueryValidator _validator;

    public GetMyDailyCheckinsQueryValidatorTests()
    {
        _validator = new GetMyDailyCheckinsQueryValidator();
    }

    [Fact]
    public void Validate_WithValidQuery_ShouldNotHaveValidationErrors()
    {
        // Arrange
        var query = new GetMyDailyCheckinsQuery(DateTime.UtcNow.Date.AddDays(-7), DateTime.UtcNow.Date, 1, 20);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithInvalidPageNumber_ShouldHaveValidationError()
    {
        // Arrange
        var query = new GetMyDailyCheckinsQuery(null, null, 0, 20);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PageNumber)
            .WithErrorCode("PAGE_NUMBER_INVALID");
    }

    [Fact]
    public void Validate_WithPageSizeTooLarge_ShouldHaveValidationError()
    {
        // Arrange
        var query = new GetMyDailyCheckinsQuery(null, null, 1, 51);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PageSize)
            .WithErrorCode("PAGE_SIZE_TOO_LARGE");
    }

    [Fact]
    public void Validate_WithInvalidDateRange_ShouldHaveValidationError()
    {
        // Arrange
        var query = new GetMyDailyCheckinsQuery(DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(-1), 1, 20);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x)
            .WithErrorCode("INVALID_DATE_RANGE");
    }
}
