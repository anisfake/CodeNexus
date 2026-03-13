using FluentValidation;

namespace CodeNexus.Application.Features.Lessons.Commands.GenerateLessonContent;

public class GenerateLessonContentCommandValidator : AbstractValidator<GenerateLessonContentCommand>
{
    public GenerateLessonContentCommandValidator()
    {
        RuleFor(x => x.LessonId)
            .NotEmpty()
            .WithMessage("Lesson ID is required")
            .WithErrorCode("LESSON_ID_REQUIRED");
    }
}
