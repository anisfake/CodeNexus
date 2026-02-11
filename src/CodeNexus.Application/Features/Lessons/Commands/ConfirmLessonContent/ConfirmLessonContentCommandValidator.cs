using FluentValidation;

namespace CodeNexus.Application.Features.Lessons.Commands.ConfirmLessonContent;

public class ConfirmLessonContentCommandValidator : AbstractValidator<ConfirmLessonContentCommand>
{
    public ConfirmLessonContentCommandValidator()
    {
        RuleFor(x => x.LessonId)
            .NotEmpty()
            .WithMessage("Lesson ID is required")
            .WithErrorCode("LESSON_ID_REQUIRED");

        RuleFor(x => x.Content)
            .NotEmpty()
            .WithMessage("Content is required")
            .WithErrorCode("CONTENT_REQUIRED");
    }
}
