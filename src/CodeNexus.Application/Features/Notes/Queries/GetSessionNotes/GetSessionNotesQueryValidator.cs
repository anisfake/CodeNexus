using FluentValidation;

namespace CodeNexus.Application.Features.Notes.Queries.GetSessionNotes;

public class GetSessionNotesQueryValidator : AbstractValidator<GetSessionNotesQuery>
{
    public GetSessionNotesQueryValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty();
    }
}
