using FluentValidation;

namespace CodeNexus.Application.Features.FocusSessions.Commands.AbandonSession;

public class AbandonSessionCommandValidator : AbstractValidator<AbandonSessionCommand>
{
    public AbandonSessionCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("SessionId is required");
    }
}