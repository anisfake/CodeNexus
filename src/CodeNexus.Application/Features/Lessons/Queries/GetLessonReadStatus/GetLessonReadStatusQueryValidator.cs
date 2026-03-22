using FluentValidation;

namespace CodeNexus.Application.Features.Lessons.Queries.GetLessonReadStatus;

public class GetLessonReadStatusQueryValidator : AbstractValidator<GetLessonReadStatusQuery>
{
    public GetLessonReadStatusQueryValidator()
    {
        RuleFor(x => x.LessonId)
            .NotEmpty()
            .WithMessage("Lesson ID is required")
            .WithErrorCode("LESSON_ID_REQUIRED");
    }
}
