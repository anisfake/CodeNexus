using FluentValidation;

namespace CodeNexus.Application.Features.DirectChats.Queries.GetConversationMessages;

public class GetConversationMessagesQueryValidator : AbstractValidator<GetConversationMessagesQuery>
{
    public GetConversationMessagesQueryValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty()
            .WithErrorCode("CONVERSATION_ID_REQUIRED")
            .WithMessage("ConversationId is required.");

        RuleFor(x => x.PageNumber)
            .GreaterThan(0)
            .WithErrorCode("INVALID_PAGE_NUMBER")
            .WithMessage("PageNumber must be greater than 0.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithErrorCode("INVALID_PAGE_SIZE")
            .WithMessage("PageSize must be between 1 and 100.");
    }
}
