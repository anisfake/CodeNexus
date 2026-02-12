using FluentValidation;

namespace CodeNexus.Application.Features.Quizzes.Commands.GenerateQuizQuestions;

public class GenerateQuizQuestionsCommandValidator : AbstractValidator<GenerateQuizQuestionsCommand>
{
    public GenerateQuizQuestionsCommandValidator()
    {
        RuleFor(x => x.QuizId)
            .NotEmpty()
            .WithMessage("Quiz ID is required")
            .WithErrorCode("QUIZ_ID_REQUIRED");
    }
}
