using CodeNexus.Application.Features.ChannelMessages.Queries.GetChannelMessages;
using CodeNexus.Domain.Enums;
using FluentValidation.TestHelper;

namespace CodeNexus.UnitTests.Features.ChannelMessages.Validators;

public class GetChannelMessagesQueryValidatorTests
{
    private readonly GetChannelMessagesQueryValidator _validator;

    public GetChannelMessagesQueryValidatorTests()
    {
        _validator = new GetChannelMessagesQueryValidator();
    }

    [Fact]
    public void Validate_InvalidPageNumber_ShouldHaveValidationError()
    {
        var query = new GetChannelMessagesQuery(SubjectCategory.Cloud, 0, 30);

        var result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(x => x.PageNumber)
            .WithErrorCode("INVALID_PAGE_NUMBER");
    }

    [Fact]
    public void Validate_InvalidPageSize_ShouldHaveValidationError()
    {
        var query = new GetChannelMessagesQuery(SubjectCategory.Cloud, 1, 101);

        var result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(x => x.PageSize)
            .WithErrorCode("INVALID_PAGE_SIZE");
    }

    [Fact]
    public void Validate_ValidQuery_ShouldNotHaveValidationErrors()
    {
        var query = new GetChannelMessagesQuery(SubjectCategory.Cloud, 1, 30);

        var result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
