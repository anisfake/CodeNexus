using FluentValidation;

namespace CodeNexus.Application.Features.MentorValidationRequests.Commands.SubmitValidationRequest;

public class SubmitValidationRequestCommandValidator : AbstractValidator<SubmitValidationRequestCommand>
{
    public SubmitValidationRequestCommandValidator()
    {
        RuleFor(x => x.PathId)
            .NotEmpty()
            .WithErrorCode("INVALID_PATH_ID")
            .WithMessage("Learning path ID is required.");

        RuleFor(x => x.MentorId)
            .NotEmpty()
            .WithErrorCode("INVALID_MENTOR_ID")
            .WithMessage("Mentor ID is required.");

        RuleFor(x => x.StudentNote)
            .MaximumLength(1000)
            .WithErrorCode("STUDENT_NOTE_TOO_LONG")
            .WithMessage("Student note must not exceed 1000 characters.")
            .When(x => x.StudentNote != null);
    }
}
