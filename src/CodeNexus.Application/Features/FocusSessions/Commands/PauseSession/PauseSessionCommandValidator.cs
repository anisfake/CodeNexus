using FluentValidation;

namespace CodeNexus.Application.Features.FocusSessions.Commands.PauseSession;

public class PauseSessionCommandValidator : AbstractValidator<PauseSessionCommand>
{
    public PauseSessionCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithErrorCode("SESSION_ID_REQUIRED")
            .WithMessage("SessionId is required.");
    }
}
