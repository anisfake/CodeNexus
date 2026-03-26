using CodeNexus.Application.Features.ChannelMessages.Queries.GetChannels;
using FluentValidation.TestHelper;
using MassTransit;

namespace CodeNexus.UnitTests.Features.ChannelMessages.Validators;

public class GetChannelsQueryValidatorTests
{
    private readonly GetChannelsQueryValidator _validator;

    public GetChannelsQueryValidatorTests()
    {
        _validator = new GetChannelsQueryValidator();
    }

    [Fact]
    public void Validate_EmptySubjectId_ShouldHaveValidationError()
    {
        var query = new GetChannelsQuery(Guid.Empty);

        var result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(x => x.SubjectId)
            .WithErrorCode("SUBJECT_ID_REQUIRED");
    }

    [Fact]
    public void Validate_ValidQuery_ShouldNotHaveValidationErrors()
    {
        var query = new GetChannelsQuery(NewId.NextGuid());

        var result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
