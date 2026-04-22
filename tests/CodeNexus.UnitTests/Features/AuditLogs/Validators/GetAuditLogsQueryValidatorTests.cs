using CodeNexus.Application.Features.AuditLogs.Queries.GetAuditLogs;
using FluentValidation.TestHelper;
using Xunit;

namespace CodeNexus.UnitTests.Features.AuditLogs.Validators;

public class GetAuditLogsQueryValidatorTests
{
    private readonly GetAuditLogsQueryValidator _validator;

    public GetAuditLogsQueryValidatorTests()
    {
        _validator = new GetAuditLogsQueryValidator();
    }

    [Fact]
    public void Validate_WithDefaultQuery_ShouldNotHaveErrors()
    {
        // Arrange
        var query = new GetAuditLogsQuery();

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithValidQuery_ShouldNotHaveErrors()
    {
        // Arrange
        var query = new GetAuditLogsQuery
        {
            PageNumber = 1,
            PageSize = 20,
            Action = "Modified",
            TableName = "Users",
            FromDate = DateTime.UtcNow.AddDays(-7),
            ToDate = DateTime.UtcNow,
            SortBy = AuditLogSortBy.Timestamp,
            SortDescending = true
        };

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_WithInvalidPageNumber_ShouldHaveError(int pageNumber)
    {
        // Arrange
        var query = new GetAuditLogsQuery { PageNumber = pageNumber };

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PageNumber)
            .WithErrorCode("INVALID_PAGE_NUMBER");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithPageSizeLessThanOrEqualZero_ShouldHaveError(int pageSize)
    {
        // Arrange
        var query = new GetAuditLogsQuery { PageSize = pageSize };

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PageSize)
            .WithErrorCode("INVALID_PAGE_SIZE");
    }

    [Fact]
    public void Validate_WithPageSizeExceeding100_ShouldHaveError()
    {
        // Arrange
        var query = new GetAuditLogsQuery { PageSize = 101 };

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PageSize)
            .WithErrorCode("INVALID_PAGE_SIZE");
    }

    [Fact]
    public void Validate_WithPageSize100_ShouldNotHaveError()
    {
        // Arrange
        var query = new GetAuditLogsQuery { PageSize = 100 };

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void Validate_WithPageSize1_ShouldNotHaveError()
    {
        // Arrange
        var query = new GetAuditLogsQuery { PageSize = 1 };

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void Validate_WithToDateBeforeFromDate_ShouldHaveError()
    {
        // Arrange
        var query = new GetAuditLogsQuery
        {
            FromDate = DateTime.UtcNow,
            ToDate = DateTime.UtcNow.AddDays(-1)
        };

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ToDate)
            .WithErrorCode("INVALID_DATE_RANGE");
    }

    [Fact]
    public void Validate_WithToDateEqualToFromDate_ShouldNotHaveError()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var query = new GetAuditLogsQuery
        {
            FromDate = now,
            ToDate = now
        };

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.ToDate);
    }

    [Fact]
    public void Validate_WithOnlyFromDate_ShouldNotHaveError()
    {
        // Arrange
        var query = new GetAuditLogsQuery { FromDate = DateTime.UtcNow.AddDays(-7) };

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithOnlyToDate_ShouldNotHaveError()
    {
        // Arrange
        var query = new GetAuditLogsQuery { ToDate = DateTime.UtcNow };

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithToDateAfterFromDate_ShouldNotHaveError()
    {
        // Arrange
        var query = new GetAuditLogsQuery
        {
            FromDate = DateTime.UtcNow.AddDays(-7),
            ToDate = DateTime.UtcNow
        };

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.ToDate);
    }

    [Fact]
    public void Validate_WithDateRangeExceeding30Days_ShouldHaveError()
    {
        // Arrange
        var query = new GetAuditLogsQuery
        {
            FromDate = DateTime.UtcNow.AddDays(-31),
            ToDate = DateTime.UtcNow
        };

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x)
            .WithErrorCode("DATE_RANGE_TOO_LARGE");
    }

    [Fact]
    public void Validate_WithDateRangeExactly30Days_ShouldNotHaveError()
    {
        // Arrange
        var toDate = DateTime.UtcNow;
        var query = new GetAuditLogsQuery
        {
            FromDate = toDate.AddDays(-30),
            ToDate = toDate
        };

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x);
    }
}
