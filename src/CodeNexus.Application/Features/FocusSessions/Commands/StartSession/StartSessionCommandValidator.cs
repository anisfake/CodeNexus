using FluentValidation;

namespace CodeNexus.Application.Features.FocusSessions.Commands.StartSession;

public class StartSessionCommandValidator : AbstractValidator<StartSessionCommand>
{
    public StartSessionCommandValidator()
    {
        RuleFor(x => x.TaskId)
            .NotEmpty()
            .WithMessage("TaskId is required");

        RuleFor(x => x.PlannedDurationMinutes)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Planned duration must be at least 1 minute");

        RuleFor(x => x.Title)
            .MaximumLength(200)
            .WithMessage("Title cannot exceed 200 characters");
    }
}