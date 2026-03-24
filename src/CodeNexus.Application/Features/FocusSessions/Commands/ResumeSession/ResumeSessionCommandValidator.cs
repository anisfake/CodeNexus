using FluentValidation;

namespace CodeNexus.Application.Features.FocusSessions.Commands.ResumeSession;

public class ResumeSessionCommandValidator : AbstractValidator<ResumeSessionCommand>
{
    public ResumeSessionCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithErrorCode("SESSION_ID_REQUIRED")
            .WithMessage("SessionId is required.");
    }
}
