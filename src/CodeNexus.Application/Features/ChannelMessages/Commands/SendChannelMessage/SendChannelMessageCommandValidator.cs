using FluentValidation;

namespace CodeNexus.Application.Features.ChannelMessages.Commands.SendChannelMessage;

public class SendChannelMessageCommandValidator : AbstractValidator<SendChannelMessageCommand>
{
    public SendChannelMessageCommandValidator()
    {
        RuleFor(x => x.SubjectId)
            .NotEmpty()
            .WithErrorCode("SUBJECT_ID_REQUIRED")
            .WithMessage("SubjectId is required.");

        RuleFor(x => x.Category)
            .IsInEnum()
            .WithErrorCode("INVALID_CATEGORY")
            .WithMessage("Category is invalid.");

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

        RuleFor(x => x.ReplyToMessageId)
            .NotEqual(Guid.Empty)
            .When(x => x.ReplyToMessageId.HasValue)
            .WithErrorCode("REPLY_TO_MESSAGE_ID_INVALID")
            .WithMessage("ReplyToMessageId is invalid.");
    }
}
