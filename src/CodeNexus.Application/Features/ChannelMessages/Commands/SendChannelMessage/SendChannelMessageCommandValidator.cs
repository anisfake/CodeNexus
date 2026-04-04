using CodeNexus.Domain.Enums;
using FluentValidation;

namespace CodeNexus.Application.Features.ChannelMessages.Commands.SendChannelMessage;

public class SendChannelMessageCommandValidator : AbstractValidator<SendChannelMessageCommand>
{
    public SendChannelMessageCommandValidator()
    {
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

        RuleFor(x => x.LearningPathShareId)
            .NotNull()
            .When(x => x.MessageType == DirectMessageType.LearningPathShare)
            .WithErrorCode("LEARNING_PATH_SHARE_ID_REQUIRED")
            .WithMessage("LearningPathShareId is required for LearningPathShare messages.");

        RuleFor(x => x.LearningPathShareId)
            .Null()
            .When(x => x.MessageType != DirectMessageType.LearningPathShare)
            .WithErrorCode("LEARNING_PATH_SHARE_ID_NOT_ALLOWED")
            .WithMessage("LearningPathShareId is only allowed for LearningPathShare messages.");

        RuleFor(x => x.LearningPathShareId)
            .NotEqual(Guid.Empty)
            .When(x => x.LearningPathShareId.HasValue)
            .WithErrorCode("LEARNING_PATH_SHARE_ID_INVALID")
            .WithMessage("LearningPathShareId is invalid.");
    }
}
