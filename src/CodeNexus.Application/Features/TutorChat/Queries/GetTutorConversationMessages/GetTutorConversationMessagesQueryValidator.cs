using FluentValidation;

namespace CodeNexus.Application.Features.TutorChat.Queries.GetTutorConversationMessages;

public class GetTutorConversationMessagesQueryValidator : AbstractValidator<GetTutorConversationMessagesQuery>
{
    public GetTutorConversationMessagesQueryValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty()
            .WithErrorCode("CONVERSATION_ID_REQUIRED")
            .WithMessage("ConversationId is required.");

        RuleFor(x => x.PageNumber)
            .GreaterThan(0);

        RuleFor(x => x.PageSize)
            .GreaterThan(0)
            .LessThanOrEqualTo(100);
    }
}
