using FluentValidation;

namespace CodeNexus.Application.Features.FocusSessions.Commands.HeartbeatSession;

public class HeartbeatSessionCommandValidator : AbstractValidator<HeartbeatSessionCommand>
{
    public HeartbeatSessionCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("SessionId is required");
    }
}
