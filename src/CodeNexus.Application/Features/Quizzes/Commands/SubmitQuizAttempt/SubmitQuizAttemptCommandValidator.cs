using FluentValidation;

namespace CodeNexus.Application.Features.Quizzes.Commands.SubmitQuizAttempt;

public class SubmitQuizAttemptCommandValidator : AbstractValidator<SubmitQuizAttemptCommand>
{
    public SubmitQuizAttemptCommandValidator()
    {
        RuleFor(x => x.AttemptId)
            .NotEmpty()
            .WithMessage("Attempt ID is required")
            .WithErrorCode("ATTEMPT_ID_REQUIRED");

        RuleFor(x => x.Answers)
            .NotNull()
            .WithMessage("Answers are required")
            .WithErrorCode("ANSWERS_REQUIRED");
    }
}
