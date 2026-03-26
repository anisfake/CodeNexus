using CodeNexus.Application.Features.ChannelMessages.Commands.SendChannelMessage;
using CodeNexus.Domain.Enums;
using FluentValidation.TestHelper;
using MassTransit;

namespace CodeNexus.UnitTests.Features.ChannelMessages.Validators;

public class SendChannelMessageCommandValidatorTests
{
    private readonly SendChannelMessageCommandValidator _validator;

    public SendChannelMessageCommandValidatorTests()
    {
        _validator = new SendChannelMessageCommandValidator();
    }

    [Fact]
    public void Validate_EmptySubjectId_ShouldHaveValidationError()
    {
        var command = new SendChannelMessageCommand(Guid.Empty, SubjectCategory.Backend, "hello");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.SubjectId)
            .WithErrorCode("SUBJECT_ID_REQUIRED");
    }

    [Fact]
    public void Validate_EmptyContent_ShouldHaveValidationError()
    {
        var command = new SendChannelMessageCommand(NewId.NextGuid(), SubjectCategory.Backend, string.Empty);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Content)
            .WithErrorCode("CONTENT_REQUIRED");
    }

    [Fact]
    public void Validate_ValidCommand_ShouldNotHaveValidationErrors()
    {
        var command = new SendChannelMessageCommand(NewId.NextGuid(), SubjectCategory.Backend, "hello channel");

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
