using FluentValidation;

namespace CodeNexus.Application.Features.Quizzes.Commands.GenerateQuizSkeleton;

public class GenerateQuizSkeletonCommandValidator : AbstractValidator<GenerateQuizSkeletonCommand>
{
    public GenerateQuizSkeletonCommandValidator()
    {
        RuleFor(x => x.LessonId)
            .NotEmpty()
            .WithMessage("LessonId is required");
    }
}