using FluentValidation;

namespace CodeNexus.Application.Features.ChannelMessages.Queries.GetChannels;

public class GetChannelsQueryValidator : AbstractValidator<GetChannelsQuery>
{
    public GetChannelsQueryValidator()
    {
        RuleFor(x => x.SubjectId)
            .NotEmpty()
            .WithErrorCode("SUBJECT_ID_REQUIRED")
            .WithMessage("SubjectId is required.");
    }
}
