using FluentValidation;

namespace CodeNexus.Application.Features.FocusSessions.Commands.ReviewSession;

public class ReviewSessionCommandValidator : AbstractValidator<ReviewSessionCommand>
{
    public ReviewSessionCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("Session ID is required");
    }
}