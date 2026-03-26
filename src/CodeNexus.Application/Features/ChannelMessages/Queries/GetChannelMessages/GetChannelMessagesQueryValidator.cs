using FluentValidation;

namespace CodeNexus.Application.Features.ChannelMessages.Queries.GetChannelMessages;

public class GetChannelMessagesQueryValidator : AbstractValidator<GetChannelMessagesQuery>
{
    public GetChannelMessagesQueryValidator()
    {
        RuleFor(x => x.SubjectId)
            .NotEmpty()
            .WithErrorCode("SUBJECT_ID_REQUIRED")
            .WithMessage("SubjectId is required.");

        RuleFor(x => x.Category)
            .IsInEnum()
            .WithErrorCode("INVALID_CATEGORY")
            .WithMessage("Category is invalid.");

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
