using FluentValidation;

namespace CodeNexus.Application.Features.Notes.Commands.DeleteSessionNote;

public class DeleteSessionNoteCommandValidator : AbstractValidator<DeleteSessionNoteCommand>
{
    public DeleteSessionNoteCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty();

        RuleFor(x => x.NoteId)
            .NotEmpty();
    }
}
