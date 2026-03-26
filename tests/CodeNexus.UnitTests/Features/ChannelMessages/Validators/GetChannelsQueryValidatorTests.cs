using CodeNexus.Application.Features.ChannelMessages.Queries.GetChannels;
using FluentValidation.TestHelper;

namespace CodeNexus.UnitTests.Features.ChannelMessages.Validators;

public class GetChannelsQueryValidatorTests
{
    private readonly GetChannelsQueryValidator _validator;

    public GetChannelsQueryValidatorTests()
    {
        _validator = new GetChannelsQueryValidator();
    }

    [Fact]
    public void Validate_Query_ShouldNotHaveValidationErrors()
    {
        var query = new GetChannelsQuery();

        var result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
