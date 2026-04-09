using FluentValidation;

namespace CodeNexus.Application.Features.Quizzes.Commands.GenerateSingleQuizQuestion;

public class GenerateSingleQuizQuestionCommandValidator : AbstractValidator<GenerateSingleQuizQuestionCommand>
{
    public GenerateSingleQuizQuestionCommandValidator()
    {
        RuleFor(x => x.QuizId)
            .NotEmpty()
            .WithMessage("QuizId is required")
            .WithErrorCode("QUIZ_ID_REQUIRED");

        RuleFor(x => x.QuestionType)
            .IsInEnum()
            .WithMessage("QuestionType is invalid")
            .WithErrorCode("QUESTION_TYPE_INVALID");
    }
}
