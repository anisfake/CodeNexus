using FluentValidation;

namespace CodeNexus.Application.Features.Lessons.Commands.MarkLessonContentRead;

public class MarkLessonContentReadCommandValidator : AbstractValidator<MarkLessonContentReadCommand>
{
    public MarkLessonContentReadCommandValidator()
    {
        RuleFor(x => x.LessonId)
            .NotEmpty()
            .WithMessage("Lesson ID is required")
            .WithErrorCode("LESSON_ID_REQUIRED");
    }
}
