using FluentValidation;

namespace CodeNexus.Application.Features.Subjects.Commands.DeleteSubject;

public class DeleteSubjectCommandValidator : AbstractValidator<DeleteSubjectCommand>
{
    public DeleteSubjectCommandValidator()
    {
        RuleFor(x => x.SubjectId)
            .NotEmpty().WithMessage("Subject ID is required.");
    }
}
