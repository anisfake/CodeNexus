using FluentValidation;

namespace CodeNexus.Application.Features.DirectChats.Commands.CreateDirectConversation;

public class CreateDirectConversationCommandValidator : AbstractValidator<CreateDirectConversationCommand>
{
    public CreateDirectConversationCommandValidator()
    {
        RuleFor(x => x.ParticipantId)
            .NotEmpty()
            .WithErrorCode("PARTICIPANT_ID_REQUIRED")
            .WithMessage("ParticipantId is required.");
    }
}
