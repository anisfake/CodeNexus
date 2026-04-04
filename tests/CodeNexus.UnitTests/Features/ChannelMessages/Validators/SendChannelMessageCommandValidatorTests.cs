using CodeNexus.Application.Features.ChannelMessages.Commands.SendChannelMessage;
using CodeNexus.Domain.Enums;
using FluentValidation.TestHelper;

namespace CodeNexus.UnitTests.Features.ChannelMessages.Validators;

public class SendChannelMessageCommandValidatorTests
{
    private readonly SendChannelMessageCommandValidator _validator;

    public SendChannelMessageCommandValidatorTests()
    {
        _validator = new SendChannelMessageCommandValidator();
    }

    [Fact]
    public void Validate_EmptyContent_ShouldHaveValidationError()
    {
        var command = new SendChannelMessageCommand(SubjectCategory.Backend, string.Empty);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Content)
            .WithErrorCode("CONTENT_REQUIRED");
    }

    [Fact]
    public void Validate_LearningPathShareTypeWithoutId_ShouldHaveValidationError()
    {
        var command = new SendChannelMessageCommand(SubjectCategory.Backend, "share", DirectMessageType.LearningPathShare);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.LearningPathShareId)
            .WithErrorCode("LEARNING_PATH_SHARE_ID_REQUIRED");
    }

    [Fact]
    public void Validate_ValidCommand_ShouldNotHaveValidationErrors()
    {
        var command = new SendChannelMessageCommand(SubjectCategory.Backend, "hello channel");

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
