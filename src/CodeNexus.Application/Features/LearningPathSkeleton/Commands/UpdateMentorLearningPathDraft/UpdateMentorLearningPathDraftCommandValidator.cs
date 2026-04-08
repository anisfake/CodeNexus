using FluentValidation;
using CodeNexus.Application.Features.LearningPaths.DTOs;

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
            .WithErrorCode("CHAPTERS_REQUIRED");

        RuleForEach(x => x.Chapters)
            .ChildRules(chapter =>
            {
                chapter.RuleFor(c => c.Title)
                    .MaximumLength(200)
                    .When(c => !string.IsNullOrWhiteSpace(c.Title))
                    .WithMessage("Chapter title must be at most 200 characters")
                    .WithErrorCode("INVALID_CHAPTER_TITLE");

                chapter.RuleForEach(c => c.Lessons)
                    .ChildRules(lesson =>
                    {
                        lesson.RuleFor(l => l.Title)
                            .MaximumLength(200)
                            .When(l => !string.IsNullOrWhiteSpace(l.Title))
                            .WithMessage("Lesson title must be at most 200 characters")
                            .WithErrorCode("INVALID_LESSON_TITLE");

                        lesson.RuleForEach(l => l.Quizzes!)
                            .ChildRules(quiz =>
                            {
                                quiz.RuleFor(q => q.Title)
                                    .MaximumLength(200)
                                    .When(q => !string.IsNullOrWhiteSpace(q.Title))
                                    .WithMessage("Quiz title must be at most 200 characters")
                                    .WithErrorCode("INVALID_QUIZ_TITLE");

                                quiz.RuleForEach(q => q.Questions!)
                                    .ChildRules(question =>
                                    {
                                        question.RuleFor(x => x.QuestionText)
                                            .MaximumLength(2000)
                                            .When(x => !string.IsNullOrWhiteSpace(x.QuestionText))
                                            .WithMessage("Question text must be at most 2000 characters")
                                            .WithErrorCode("INVALID_QUESTION_TEXT");

                                        question.RuleFor(x => x.Type)
                                            .IsInEnum()
                                            .WithMessage("Question type is invalid")
                                            .WithErrorCode("INVALID_QUESTION_TYPE");

                                        question.RuleFor(x => x.Points)
                                            .GreaterThan(0)
                                            .WithMessage("Question points must be greater than 0")
                                            .WithErrorCode("INVALID_QUESTION_POINTS");
                                    })
                                    .When(q => q.Questions is not null);
                            })
                            .When(l => l.Quizzes is not null);
                    });

                chapter.RuleForEach(c => c.Tasks!)
                    .ChildRules(task =>
                    {
                        task.RuleFor(t => t.Title)
                            .MaximumLength(200)
                            .When(t => !string.IsNullOrWhiteSpace(t.Title))
                            .WithMessage("Task title must be at most 200 characters")
                            .WithErrorCode("INVALID_TASK_TITLE");

                        task.RuleFor(t => t.TaskType)
                            .IsInEnum()
                            .WithMessage("Task type is invalid")
                            .WithErrorCode("INVALID_TASK_TYPE");
                    })
                    .When(c => c.Tasks is not null);
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
