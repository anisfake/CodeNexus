using FluentValidation;

namespace CodeNexus.Application.Features.Quizzes.Commands.StartQuizAttempt;

public class StartQuizAttemptCommandValidator : AbstractValidator<StartQuizAttemptCommand>
{
    public StartQuizAttemptCommandValidator()
    {
        RuleFor(x => x.QuizId)
            .NotEmpty()
            .WithMessage("Quiz ID is required")
            .WithErrorCode("QUIZ_ID_REQUIRED");
    }
}
