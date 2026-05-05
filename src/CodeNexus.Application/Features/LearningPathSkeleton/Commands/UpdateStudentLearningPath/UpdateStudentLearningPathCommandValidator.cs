using FluentValidation;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Commands.UpdateStudentLearningPath;

public class UpdateStudentLearningPathCommandValidator : AbstractValidator<UpdateStudentLearningPathCommand>
{
    public UpdateStudentLearningPathCommandValidator()
    {
        RuleFor(x => x.PathId)
            .NotEmpty()
            .WithMessage("Path ID is required.")
            .WithErrorCode("PATH_REQUIRED");

        RuleFor(x => x.Chapters)
            .NotNull()
            .NotEmpty()
            .WithMessage("At least one chapter is required.")
            .WithErrorCode("CHAPTERS_REQUIRED");

        RuleForEach(x => x.Chapters)
            .ChildRules(chapter =>
            {
                chapter.RuleFor(c => c.Title)
                    .NotEmpty()
                    .WithMessage("Chapter title is required.")
                    .WithErrorCode("CHAPTER_TITLE_REQUIRED");

                chapter.RuleFor(c => c.Lessons)
                    .NotNull()
                    .NotEmpty()
                    .WithMessage("At least one lesson is required per chapter.")
                    .WithErrorCode("LESSONS_REQUIRED");

                chapter.RuleForEach(c => c.Lessons)
                    .ChildRules(lesson =>
                    {
                        lesson.RuleFor(l => l.Title)
                            .NotEmpty()
                            .WithMessage("Lesson title is required.")
                            .WithErrorCode("LESSON_TITLE_REQUIRED");
                    });
            });
    }
}
