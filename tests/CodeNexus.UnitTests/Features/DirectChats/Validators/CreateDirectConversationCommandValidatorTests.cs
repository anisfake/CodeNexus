using CodeNexus.Application.Features.DirectChats.Commands.CreateDirectConversation;
using FluentValidation.TestHelper;
using MassTransit;

namespace CodeNexus.UnitTests.Features.DirectChats.Validators;

public class CreateDirectConversationCommandValidatorTests
{
    private readonly CreateDirectConversationCommandValidator _validator;

    public CreateDirectConversationCommandValidatorTests()
    {
        _validator = new CreateDirectConversationCommandValidator();
    }

    [Fact]
    public void Validate_EmptyParticipantId_ShouldHaveValidationError()
    {
        var command = new CreateDirectConversationCommand(Guid.Empty);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.ParticipantId)
            .WithErrorCode("PARTICIPANT_ID_REQUIRED");
    }

    [Fact]
    public void Validate_ValidParticipantId_ShouldNotHaveValidationError()
    {
        var command = new CreateDirectConversationCommand(NewId.NextGuid());

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.ParticipantId);
    }
}
