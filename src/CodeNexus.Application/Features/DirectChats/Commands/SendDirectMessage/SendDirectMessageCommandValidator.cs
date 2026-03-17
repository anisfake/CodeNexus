using FluentValidation;

namespace CodeNexus.Application.Features.DirectChats.Commands.SendDirectMessage;

public class SendDirectMessageCommandValidator : AbstractValidator<SendDirectMessageCommand>
{
    public SendDirectMessageCommandValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty()
            .WithErrorCode("CONVERSATION_ID_REQUIRED")
            .WithMessage("ConversationId is required.");

        RuleFor(x => x.Content)
            .NotEmpty()
            .WithErrorCode("CONTENT_REQUIRED")
            .WithMessage("Message content is required.")
            .MaximumLength(2000)
            .WithErrorCode("CONTENT_TOO_LONG")
            .WithMessage("Message content cannot exceed 2000 characters.");

        RuleFor(x => x.MessageType)
            .IsInEnum()
            .WithErrorCode("INVALID_MESSAGE_TYPE")
            .WithMessage("MessageType is invalid.");
    }
}
