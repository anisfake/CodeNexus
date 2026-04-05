using FluentValidation;

namespace CodeNexus.Application.Features.TutorChat.Queries.GetTutorConversationSummaries;

public class GetTutorConversationSummariesQueryValidator : AbstractValidator<GetTutorConversationSummariesQuery>
{
    public GetTutorConversationSummariesQueryValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty().WithMessage("ConversationId is required.");

        RuleFor(x => x.PageNumber)
            .GreaterThan(0);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50);
    }
}
