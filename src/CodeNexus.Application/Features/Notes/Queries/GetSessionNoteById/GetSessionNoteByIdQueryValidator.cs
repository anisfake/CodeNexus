using FluentValidation;

namespace CodeNexus.Application.Features.Notes.Queries.GetSessionNoteById;

public class GetSessionNoteByIdQueryValidator : AbstractValidator<GetSessionNoteByIdQuery>
{
    public GetSessionNoteByIdQueryValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty();

        RuleFor(x => x.NoteId)
            .NotEmpty();
    }
}
