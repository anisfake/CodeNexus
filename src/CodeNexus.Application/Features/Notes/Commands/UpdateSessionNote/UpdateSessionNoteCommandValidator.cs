using FluentValidation;

namespace CodeNexus.Application.Features.Notes.Commands.UpdateSessionNote;

public class UpdateSessionNoteCommandValidator : AbstractValidator<UpdateSessionNoteCommand>
{
    public UpdateSessionNoteCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty();

        RuleFor(x => x.NoteId)
            .NotEmpty();

        RuleFor(x => x.Title)
            .MaximumLength(200);

        RuleFor(x => x.Content)
            .NotEmpty()
            .MaximumLength(5000);
    }
}
