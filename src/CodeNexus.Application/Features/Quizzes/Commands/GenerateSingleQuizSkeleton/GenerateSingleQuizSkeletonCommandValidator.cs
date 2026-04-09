using FluentValidation;

namespace CodeNexus.Application.Features.Quizzes.Commands.GenerateSingleQuizSkeleton;

public class GenerateSingleQuizSkeletonCommandValidator : AbstractValidator<GenerateSingleQuizSkeletonCommand>
{
    public GenerateSingleQuizSkeletonCommandValidator()
    {
        RuleFor(x => x.LessonId)
            .NotEmpty()
            .WithMessage("LessonId is required")
            .WithErrorCode("LESSON_ID_REQUIRED");
    }
}
