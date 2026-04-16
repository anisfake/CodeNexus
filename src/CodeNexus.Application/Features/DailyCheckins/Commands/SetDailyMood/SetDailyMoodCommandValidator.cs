using FluentValidation;

namespace CodeNexus.Application.Features.DailyCheckin.Commands.SetDailyMood;

public class SetDailyMoodCommandValidator : AbstractValidator<SetDailyMoodCommand>
{
    public SetDailyMoodCommandValidator()
    {
        RuleFor(x => x.Mood)
            .NotEmpty()
            .WithErrorCode("MOOD_REQUIRED")
            .WithMessage("Mood is required.")
            .MaximumLength(50)
            .WithErrorCode("MOOD_TOO_LONG")
            .WithMessage("Mood must not exceed 50 characters.");
    }
}
