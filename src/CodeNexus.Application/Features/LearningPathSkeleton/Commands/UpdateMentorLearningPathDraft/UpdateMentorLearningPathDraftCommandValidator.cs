using FluentValidation;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Commands.UpdateMentorLearningPathDraft;

public class UpdateMentorLearningPathDraftCommandValidator : AbstractValidator<UpdateMentorLearningPathDraftCommand>
{
    public UpdateMentorLearningPathDraftCommandValidator()
    {
        RuleFor(x => x.PathId)
            .NotEmpty()
            .WithMessage("Path ID is required")
            .WithErrorCode("PATH_REQUIRED");

        RuleFor(x => x.SubjectId)
            .NotEmpty()
            .WithMessage("Subject ID is required")
            .WithErrorCode("SUBJECT_REQUIRED");

        RuleFor(x => x.Goals)
            .NotNull()
            .Must(g => g != null && g.Count >= 1 && g.Count <= 2)
            .WithMessage("Please select between 1 and 2 goals")
            .WithErrorCode("INVALID_GOALS");

        RuleFor(x => x.Goals)
            .Must(goals => goals == null || goals.Select(g => g.GoalId).Distinct().Count() == goals.Count)
            .WithMessage("Duplicate goals are not allowed")
            .WithErrorCode("DUPLICATE_GOALS");

        RuleForEach(x => x.Goals)
            .ChildRules(goal =>
            {
                goal.RuleFor(g => g.GoalId)
                    .NotEmpty()
                    .WithMessage("Goal ID is required")
                    .WithErrorCode("GOAL_REQUIRED");

                goal.RuleFor(g => g.Weight)
                    .GreaterThan(0)
                    .WithMessage("Goal weight must be greater than 0")
                    .WithErrorCode("INVALID_GOAL_WEIGHT");
            });

        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("Title is required and must be at most 200 characters")
            .WithErrorCode("INVALID_TITLE");

        RuleFor(x => x.StartDate)
            .LessThanOrEqualTo(x => x.EndDate)
            .WithMessage("Start date must be less than or equal to end date")
            .WithErrorCode("INVALID_DATE_RANGE");

        RuleFor(x => x.Chapters)
            .NotNull()
            .Must(c => c != null && c.Count > 0)
            .WithMessage("At least one chapter is required")
            .WithErrorCode("CHAPTERS_REQUIRED");

        RuleForEach(x => x.Chapters)
            .ChildRules(chapter =>
            {
                chapter.RuleFor(c => c.Title)
                    .NotEmpty()
                    .MaximumLength(200)
                    .WithMessage("Chapter title is required")
                    .WithErrorCode("INVALID_CHAPTER_TITLE");

                chapter.RuleFor(c => c.Lessons)
                    .NotNull()
                    .Must(l => l != null && l.Count > 0)
                    .WithMessage("Each chapter must have at least one lesson")
                    .WithErrorCode("LESSONS_REQUIRED");

                chapter.RuleForEach(c => c.Lessons)
                    .ChildRules(lesson =>
                    {
                        lesson.RuleFor(l => l.Title)
                            .NotEmpty()
                            .MaximumLength(200)
                            .WithMessage("Lesson title is required")
                            .WithErrorCode("INVALID_LESSON_TITLE");
                    });
            });

        RuleFor(x => x.ComplexityLevel)
            .IsInEnum()
            .WithMessage("Complexity level must be Beginner, Intermediate, or Advanced")
            .WithErrorCode("INVALID_COMPLEXITY");

        RuleFor(x => x.LanguageSelection)
            .IsInEnum()
            .WithMessage("Language selection must be Vietnamese or English")
            .WithErrorCode("INVALID_LANGUAGE");
    }
}
