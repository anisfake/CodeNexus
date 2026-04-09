using FluentValidation;

namespace CodeNexus.Application.Features.Notes.Commands.CreateSessionNote;

public class CreateSessionNoteCommandValidator : AbstractValidator<CreateSessionNoteCommand>
{
    public CreateSessionNoteCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty();

        RuleFor(x => x.Title)
            .MaximumLength(200);

        RuleFor(x => x.Content)
            .NotEmpty()
            .MaximumLength(5000);
    }
}
