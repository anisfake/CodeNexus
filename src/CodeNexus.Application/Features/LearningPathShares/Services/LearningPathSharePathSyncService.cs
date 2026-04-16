using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using TaskEntity = CodeNexus.Domain.Entities.Tasks;

namespace CodeNexus.Application.Features.LearningPathShares.Services;

public class LearningPathSharePathSyncService : ILearningPathSharePathSyncService
{
    private readonly IApplicationDbContext _context;

    public LearningPathSharePathSyncService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> ClonePathForStudentAsync(
        LearningPath sourcePath,
        Guid studentId,
        DateTime acceptedAt,
        CancellationToken cancellationToken)
    {
        var timelineAnchor = ResolveTimelineAnchor(sourcePath) ?? acceptedAt;
        var timelineShift = acceptedAt - timelineAnchor;

        DateTime? ShiftNullable(DateTime? value) => value.HasValue ? value.Value.Add(timelineShift) : null;
        DateTime Shift(DateTime value) => value.Add(timelineShift);

        var newPathId = NewId.NextGuid();
        var studentPath = new LearningPath
        {
            PathId = newPathId,
            UserId = studentId,
            SubjectId = sourcePath.SubjectId,
            Title = sourcePath.Title,
            Description = sourcePath.Description,
            StartDate = acceptedAt,
            EndDate = ShiftNullable(sourcePath.EndDate),
            Status = LearningPathStatus.Active.ToString(),
            CreatedAt = acceptedAt,
            VersionNumber = sourcePath.VersionNumber,
            CreatedByType = sourcePath.CreatedByType,
            Language = sourcePath.Language,
            ComplexityLevel = sourcePath.ComplexityLevel
        };

        await _context.LearningPaths.AddAsync(studentPath, cancellationToken);

        foreach (var goal in sourcePath.LearningPathGoals)
        {
            await _context.LearningPathGoals.AddAsync(new LearningPathGoal
            {
                PathId = newPathId,
                GoalId = goal.GoalId,
                Weight = goal.Weight
            }, cancellationToken);
        }

        foreach (var sourceChapter in sourcePath.Chapters.OrderBy(c => c.OrderIndex))
        {
            var newChapterId = NewId.NextGuid();
            await _context.Chapters.AddAsync(new Chapter
            {
                ChapterId = newChapterId,
                PathId = newPathId,
                Title = sourceChapter.Title,
                Content = sourceChapter.Content,
                OrderIndex = sourceChapter.OrderIndex,
                IsCompleted = false,
                StartDate = ShiftNullable(sourceChapter.StartDate),
                EndDate = ShiftNullable(sourceChapter.EndDate),
                EstimatedDays = sourceChapter.EstimatedDays,
                CreatedAt = acceptedAt
            }, cancellationToken);

            foreach (var sourceLesson in sourceChapter.Lessons.OrderBy(l => l.OrderIndex))
            {
                var newLessonId = NewId.NextGuid();
                await _context.Lessons.AddAsync(new Lesson
                {
                    LessonId = newLessonId,
                    ChapterId = newChapterId,
                    Title = sourceLesson.Title,
                    Content = sourceLesson.Content,
                    OrderIndex = sourceLesson.OrderIndex,
                    LessonDay = Shift(sourceLesson.LessonDay),
                    CreatedAt = acceptedAt
                }, cancellationToken);

                foreach (var sourceQuiz in sourceLesson.Quizzes)
                {
                    await _context.Quizzes.AddAsync(new Quiz
                    {
                        QuizId = NewId.NextGuid(),
                        LessonId = newLessonId,
                        Title = sourceQuiz.Title,
                        Description = sourceQuiz.Description,
                        TimeLimit = sourceQuiz.TimeLimit,
                        PassingScore = sourceQuiz.PassingScore,
                        DueDate = ShiftNullable(sourceQuiz.DueDate),
                        CreatedAt = acceptedAt
                    }, cancellationToken);
                }
            }

            foreach (var sourceTask in sourceChapter.Tasks.Where(t => !t.IsDeleted))
            {
                await _context.Tasks.AddAsync(new TaskEntity
                {
                    TaskId = NewId.NextGuid(),
                    ChapterId = newChapterId,
                    PathId = newPathId,
                    Title = sourceTask.Title,
                    Description = sourceTask.Description,
                    DueDate = ShiftNullable(sourceTask.DueDate),
                    Priority = sourceTask.Priority,
                    Status = sourceTask.Status,
                    CreatedAt = acceptedAt,
                    CompletedAt = sourceTask.CompletedAt,
                    TaskType = sourceTask.TaskType,
                    VerificationPrompt = sourceTask.VerificationPrompt,
                    MinimumScore = sourceTask.MinimumScore,
                    QuizQuestionsJson = sourceTask.QuizQuestionsJson
                }, cancellationToken);
            }
        }

        return newPathId;
    }

    public async Task RebuildCurrentPathFromSourceAsync(
        LearningPath currentPath,
        LearningPath sourcePath,
        Guid studentId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var targetStart = currentPath.StartDate ?? now;
        var sourceAnchor = ResolveTimelineAnchor(sourcePath) ?? targetStart;
        var timelineShift = targetStart - sourceAnchor;

        DateTime? ShiftNullable(DateTime? value) => value.HasValue ? value.Value.Add(timelineShift) : null;
        DateTime Shift(DateTime value) => value.Add(timelineShift);

        currentPath.SubjectId = sourcePath.SubjectId;
        currentPath.Title = sourcePath.Title;
        currentPath.Description = sourcePath.Description;
        currentPath.StartDate = targetStart;
        currentPath.EndDate = ShiftNullable(sourcePath.EndDate);
        currentPath.Status = LearningPathStatus.Active.ToString();
        currentPath.VersionNumber = sourcePath.VersionNumber;
        currentPath.CreatedByType = sourcePath.CreatedByType;
        currentPath.Language = sourcePath.Language;
        currentPath.ComplexityLevel = sourcePath.ComplexityLevel;

        _context.LearningPathGoals.RemoveRange(currentPath.LearningPathGoals);
        foreach (var goal in sourcePath.LearningPathGoals)
        {
            await _context.LearningPathGoals.AddAsync(new LearningPathGoal
            {
                PathId = currentPath.PathId,
                GoalId = goal.GoalId,
                Weight = goal.Weight
            }, cancellationToken);
        }

        var existingChaptersByOrder = currentPath.Chapters
            .Where(c => !c.IsDeleted)
            .ToDictionary(c => c.OrderIndex);

        var sourceChapters = sourcePath.Chapters
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.OrderIndex)
            .ToList();

        var processedChapterOrders = new HashSet<int>();

        foreach (var sourceChapter in sourceChapters)
        {
            processedChapterOrders.Add(sourceChapter.OrderIndex);

            Chapter studentChapter;
            if (existingChaptersByOrder.TryGetValue(sourceChapter.OrderIndex, out var matched))
            {
                studentChapter = matched;
                studentChapter.Title = sourceChapter.Title;
                studentChapter.Content = sourceChapter.Content;
                studentChapter.StartDate = ShiftNullable(sourceChapter.StartDate);
                studentChapter.EndDate = ShiftNullable(sourceChapter.EndDate);
                studentChapter.EstimatedDays = sourceChapter.EstimatedDays;
                studentChapter.UpdatedAt = now;
                studentChapter.IsDeleted = false;
                studentChapter.DeletedAt = null;
            }
            else
            {
                studentChapter = new Chapter
                {
                    ChapterId = NewId.NextGuid(),
                    PathId = currentPath.PathId,
                    Title = sourceChapter.Title,
                    Content = sourceChapter.Content,
                    OrderIndex = sourceChapter.OrderIndex,
                    IsCompleted = false,
                    StartDate = ShiftNullable(sourceChapter.StartDate),
                    EndDate = ShiftNullable(sourceChapter.EndDate),
                    EstimatedDays = sourceChapter.EstimatedDays,
                    CreatedAt = now
                };
                await _context.Chapters.AddAsync(studentChapter, cancellationToken);
            }

            await SyncStudentLessonsAsync(studentChapter, sourceChapter, studentId, now, Shift, ShiftNullable, cancellationToken);
            await SyncStudentTasksAsync(studentChapter, sourceChapter, currentPath.PathId, now, ShiftNullable, cancellationToken);
        }

        foreach (var chapter in currentPath.Chapters.Where(c => !c.IsDeleted && !processedChapterOrders.Contains(c.OrderIndex)))
        {
            chapter.IsDeleted = true;
            chapter.DeletedAt = now;
            chapter.UpdatedAt = now;

            foreach (var lesson in chapter.Lessons.Where(l => !l.IsDeleted))
            {
                lesson.IsDeleted = true;
                lesson.DeletedAt = now;
                lesson.UpdatedAt = now;
            }
        }
    }

    private async Task SyncStudentLessonsAsync(
        Chapter studentChapter,
        Chapter sourceChapter,
        Guid studentId,
        DateTime now,
        Func<DateTime, DateTime> shift,
        Func<DateTime?, DateTime?> shiftNullable,
        CancellationToken cancellationToken)
    {
        var existingLessonsByOrder = studentChapter.Lessons
            .Where(l => !l.IsDeleted)
            .ToDictionary(l => l.OrderIndex);

        var sourceLessons = sourceChapter.Lessons
            .Where(l => !l.IsDeleted)
            .OrderBy(l => l.OrderIndex)
            .ToList();

        var processedLessonOrders = new HashSet<int>();

        foreach (var sourceLesson in sourceLessons)
        {
            processedLessonOrders.Add(sourceLesson.OrderIndex);

            if (existingLessonsByOrder.TryGetValue(sourceLesson.OrderIndex, out var studentLesson))
            {
                var oldContent = studentLesson.Content?.Trim() ?? string.Empty;
                var newContent = sourceLesson.Content?.Trim() ?? string.Empty;
                var contentChanged = !string.Equals(oldContent, newContent, StringComparison.OrdinalIgnoreCase);

                studentLesson.Title = sourceLesson.Title;
                studentLesson.LessonDay = shift(sourceLesson.LessonDay);
                if (sourceLesson.Content is not null)
                {
                    studentLesson.Content = sourceLesson.Content;
                }
                studentLesson.UpdatedAt = now;
                studentLesson.IsDeleted = false;
                studentLesson.DeletedAt = null;

                SyncStudentQuizzes(studentLesson, sourceLesson, now, shiftNullable);

                if (contentChanged)
                {
                    var progress = await _context.LearnProgresses
                        .FirstOrDefaultAsync(p => p.LessonId == studentLesson.LessonId && p.UserId == studentId, cancellationToken);
                    if (progress != null)
                    {
                        progress.IsLessonContentRead = false;
                        progress.UpdatedAt = now;
                    }
                }
            }
            else
            {
                var newLessonId = NewId.NextGuid();
                await _context.Lessons.AddAsync(new Lesson
                {
                    LessonId = newLessonId,
                    ChapterId = studentChapter.ChapterId,
                    Title = sourceLesson.Title,
                    Content = sourceLesson.Content ?? string.Empty,
                    OrderIndex = sourceLesson.OrderIndex,
                    LessonDay = shift(sourceLesson.LessonDay),
                    CreatedAt = now
                }, cancellationToken);

                foreach (var sourceQuiz in sourceLesson.Quizzes.Where(q => !q.IsDeleted))
                {
                    await _context.Quizzes.AddAsync(new Quiz
                    {
                        QuizId = NewId.NextGuid(),
                        LessonId = newLessonId,
                        Title = sourceQuiz.Title,
                        Description = sourceQuiz.Description,
                        TimeLimit = sourceQuiz.TimeLimit,
                        PassingScore = sourceQuiz.PassingScore,
                        DueDate = shiftNullable(sourceQuiz.DueDate),
                        CreatedAt = now
                    }, cancellationToken);
                }
            }
        }

        foreach (var lesson in studentChapter.Lessons.Where(l => !l.IsDeleted && !processedLessonOrders.Contains(l.OrderIndex)))
        {
            lesson.IsDeleted = true;
            lesson.DeletedAt = now;
            lesson.UpdatedAt = now;
        }
    }

    private void SyncStudentQuizzes(
        Lesson studentLesson,
        Lesson sourceLesson,
        DateTime now,
        Func<DateTime?, DateTime?> shiftNullable)
    {
        var existingQuizzes = studentLesson.Quizzes
            .Where(q => !q.IsDeleted)
            .OrderBy(q => q.CreatedAt)
            .ToList();

        var sourceQuizzes = sourceLesson.Quizzes
            .Where(q => !q.IsDeleted)
            .OrderBy(q => q.CreatedAt)
            .ToList();

        for (int i = 0; i < sourceQuizzes.Count; i++)
        {
            var source = sourceQuizzes[i];
            if (i < existingQuizzes.Count)
            {
                var existing = existingQuizzes[i];
                existing.Title = source.Title;
                existing.Description = source.Description;
                existing.TimeLimit = source.TimeLimit;
                existing.PassingScore = source.PassingScore;
                existing.DueDate = shiftNullable(source.DueDate);
                existing.IsDeleted = false;
                existing.DeletedAt = null;
            }
            else
            {
                studentLesson.Quizzes.Add(new Quiz
                {
                    QuizId = NewId.NextGuid(),
                    LessonId = studentLesson.LessonId,
                    Title = source.Title,
                    Description = source.Description,
                    TimeLimit = source.TimeLimit,
                    PassingScore = source.PassingScore,
                    DueDate = shiftNullable(source.DueDate),
                    CreatedAt = now
                });
            }
        }

        foreach (var quiz in existingQuizzes.Skip(sourceQuizzes.Count))
        {
            quiz.IsDeleted = true;
            quiz.DeletedAt = now;
        }
    }

    private async Task SyncStudentTasksAsync(
        Chapter studentChapter,
        Chapter sourceChapter,
        Guid pathId,
        DateTime now,
        Func<DateTime?, DateTime?> shiftNullable,
        CancellationToken cancellationToken)
    {
        var existingTasks = studentChapter.Tasks
            .Where(t => !t.IsDeleted)
            .OrderBy(t => t.CreatedAt)
            .ToList();

        var sourceTasks = sourceChapter.Tasks
            .Where(t => !t.IsDeleted)
            .OrderBy(t => t.CreatedAt)
            .ToList();

        for (int i = 0; i < sourceTasks.Count; i++)
        {
            var source = sourceTasks[i];
            if (i < existingTasks.Count)
            {
                var existing = existingTasks[i];
                existing.Title = source.Title;
                existing.Description = source.Description;
                existing.DueDate = shiftNullable(source.DueDate);
                existing.Priority = source.Priority;
                existing.TaskType = source.TaskType;
                existing.VerificationPrompt = source.VerificationPrompt;
                existing.MinimumScore = source.MinimumScore;
                existing.QuizQuestionsJson = source.QuizQuestionsJson;
                existing.UpdatedAt = now;
            }
            else
            {
                await _context.Tasks.AddAsync(new TaskEntity
                {
                    TaskId = NewId.NextGuid(),
                    ChapterId = studentChapter.ChapterId,
                    PathId = pathId,
                    Title = source.Title,
                    Description = source.Description,
                    DueDate = shiftNullable(source.DueDate),
                    Priority = source.Priority,
                    Status = source.Status,
                    CreatedAt = now,
                    TaskType = source.TaskType,
                    VerificationPrompt = source.VerificationPrompt,
                    MinimumScore = source.MinimumScore,
                    QuizQuestionsJson = source.QuizQuestionsJson
                }, cancellationToken);
            }
        }

        foreach (var task in existingTasks.Skip(sourceTasks.Count))
        {
            task.IsDeleted = true;
            task.DeletedAt = now;
            task.UpdatedAt = now;
        }
    }

    private static DateTime? ResolveTimelineAnchor(LearningPath sourcePath)
    {
        if (sourcePath.StartDate.HasValue)
        {
            return sourcePath.StartDate.Value;
        }

        DateTime? earliest = sourcePath.EndDate;

        foreach (var chapter in sourcePath.Chapters)
        {
            earliest = MinDate(earliest, chapter.StartDate);
            earliest = MinDate(earliest, chapter.EndDate);

            foreach (var lesson in chapter.Lessons)
            {
                earliest = MinDate(earliest, lesson.LessonDay);

                foreach (var quiz in lesson.Quizzes)
                {
                    earliest = MinDate(earliest, quiz.DueDate);
                }
            }

            foreach (var task in chapter.Tasks)
            {
                earliest = MinDate(earliest, task.DueDate);
            }
        }

        return earliest;
    }

    private static DateTime? MinDate(DateTime? current, DateTime? candidate)
    {
        if (!candidate.HasValue)
        {
            return current;
        }

        if (!current.HasValue || candidate.Value < current.Value)
        {
            return candidate.Value;
        }

        return current;
    }
}