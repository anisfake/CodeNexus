using FluentValidation;

namespace CodeNexus.Application.Features.FocusSessions.Commands.CompleteSession;

public class CompleteSessionCommandValidator : AbstractValidator<CompleteSessionCommand>
{
    public CompleteSessionCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("SessionId is required");

        RuleFor(x => x.SubmittedCode)
            .MaximumLength(10000)
            .WithMessage("Submitted code cannot exceed 10,000 characters")
            .Must(BeValidCodeContent)
            .WithMessage("Submitted code contains invalid characters")
            .When(x => !string.IsNullOrEmpty(x.SubmittedCode));

        RuleFor(x => x.SubmittedSummary)
            .MaximumLength(2000)
            .WithMessage("Submitted summary cannot exceed 2,000 characters")
            .When(x => !string.IsNullOrEmpty(x.SubmittedSummary));
    }

    private static bool BeValidCodeContent(string? code)
    {
        if (string.IsNullOrEmpty(code))
            return true;

        return !code.Contains('\0');
    }
}