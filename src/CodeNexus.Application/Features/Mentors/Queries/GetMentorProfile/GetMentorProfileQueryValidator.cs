using FluentValidation;

namespace CodeNexus.Application.Features.Mentors.Queries.GetMentorProfile;

public class GetMentorProfileQueryValidator : AbstractValidator<GetMentorProfileQuery>
{
    public GetMentorProfileQueryValidator()
    {
        RuleFor(x => x.MentorId)
            .NotEmpty()
            .WithErrorCode("MENTOR_ID_REQUIRED")
            .WithMessage("MentorId is required.");
    }
}
