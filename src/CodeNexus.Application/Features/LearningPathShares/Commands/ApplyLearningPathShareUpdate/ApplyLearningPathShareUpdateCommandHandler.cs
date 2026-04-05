using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPathShares.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TaskEntity = CodeNexus.Domain.Entities.Tasks;

namespace CodeNexus.Application.Features.LearningPathShares.Commands.ApplyLearningPathShareUpdate;

public class ApplyLearningPathShareUpdateCommandHandler : IRequestHandler<ApplyLearningPathShareUpdateCommand, Result<LearningPathShareDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public ApplyLearningPathShareUpdateCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<LearningPathShareDto>> Handle(ApplyLearningPathShareUpdateCommand request, CancellationToken cancellationToken)
    {
        Guid studentId;
        try
        {
            studentId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<LearningPathShareDto>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        var share = await _context.LearningPathShares
            .FirstOrDefaultAsync(s => s.ShareId == request.ShareId && s.StudentId == studentId, cancellationToken);

        if (share == null)
        {
            return Result<LearningPathShareDto>.Failure("SHARE_NOT_FOUND", "Learning path share not found.");
        }

        if (share.Status != LearningPathShareStatus.Accepted)
        {
            return Result<LearningPathShareDto>.Failure("INVALID_SHARE_STATE", "Only accepted shares can be updated.");
        }

        var sourcePath = await _context.LearningPaths
            .AsNoTracking()
            .Include(lp => lp.LearningPathGoals)
            .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                .ThenInclude(c => c.Lessons.Where(l => !l.IsDeleted))
                .ThenInclude(l => l.Quizzes.Where(q => !q.IsDeleted))
            .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                .ThenInclude(c => c.Tasks)
            .FirstOrDefaultAsync(lp => lp.PathId == share.PathId, cancellationToken);

        if (sourcePath == null)
        {
            return Result<LearningPathShareDto>.Failure("SOURCE_LEARNING_PATH_NOT_FOUND", "Source learning path not found.");
        }

        var latestVersion = sourcePath.VersionNumber;
        var currentSourceVersion = share.SourceVersionAtAccept ?? 1;
        var hasNewVersion = latestVersion > currentSourceVersion;

        if (!hasNewVersion)
        {
            return Result<LearningPathShareDto>.Failure("NO_NEW_VERSION_AVAILABLE", "No new version is available.");
        }

        switch (request.Action)
        {
            case LearningPathShareUpdateAction.CreateNewFromLatest:
            {
                var newPathId = await ClonePathForStudentAsync(sourcePath, studentId, DateTime.UtcNow.AddHours(7), cancellationToken);
                share.AcceptedPathId = newPathId;
                share.SourceVersionAtAccept = latestVersion;
                share.IgnoredSourceVersion = null;
                share.LastNotifiedSourceVersion = latestVersion;
                share.IsTrackingEnabled = true;
                break;
            }
            case LearningPathShareUpdateAction.UpdateCurrentToLatest:
            {
                if (!share.AcceptedPathId.HasValue)
                {
                    return Result<LearningPathShareDto>.Failure("LEARNING_PATH_NOT_FOUND", "Accepted learning path not found.");
                }

                var acceptedPath = await _context.LearningPaths
                    .Include(lp => lp.LearningPathGoals)
                    .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                        .ThenInclude(c => c.Lessons.Where(l => !l.IsDeleted))
                    .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                        .ThenInclude(c => c.Tasks)
                    .FirstOrDefaultAsync(lp => lp.PathId == share.AcceptedPathId.Value && lp.UserId == studentId, cancellationToken);

                if (acceptedPath == null)
                {
                    return Result<LearningPathShareDto>.Failure("LEARNING_PATH_NOT_FOUND", "Accepted learning path not found.");
                }

                await RebuildCurrentPathFromSourceAsync(acceptedPath, sourcePath, studentId, DateTime.UtcNow.AddHours(7), cancellationToken);
                share.SourceVersionAtAccept = latestVersion;
                share.IgnoredSourceVersion = null;
                share.LastNotifiedSourceVersion = latestVersion;
                share.IsTrackingEnabled = true;
                break;
            }
            case LearningPathShareUpdateAction.KeepCurrent:
            {
                share.IgnoredSourceVersion = latestVersion;
                share.LastNotifiedSourceVersion = latestVersion;
                share.IsTrackingEnabled = true;
                break;
            }
            default:
                return Result<LearningPathShareDto>.Failure("INVALID_ACTION", "Invalid update action.");
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result<LearningPathShareDto>.Success(new LearningPathShareDto(
            share.ShareId,
            share.PathId,
            share.MentorId,
            share.StudentId,
            share.Status,
            share.SentAt,
            share.RespondedAt,
            share.AcceptedPathId,
            share.SourceVersionAtAccept,
            share.IgnoredSourceVersion,
            share.LastNotifiedSourceVersion,
            share.IsTrackingEnabled,
            share.InvalidatedReason
        ));
    }

    private async Task<Guid> ClonePathForStudentAsync(LearningPath sourcePath, Guid studentId, DateTime acceptedAt, CancellationToken cancellationToken)
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

            foreach (var sourceTask in sourceChapter.Tasks)
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

    private async Task RebuildCurrentPathFromSourceAsync(
        LearningPath currentPath,
        LearningPath sourcePath,
        Guid studentId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var oldLessonIds = currentPath.Chapters
            .SelectMany(c => c.Lessons)
            .Select(l => l.LessonId)
            .ToList();

        var oldLessonProgressByLessonId = await _context.LearnProgresses
            .AsNoTracking()
            .Where(p => p.UserId == studentId && p.IsLessonContentRead && oldLessonIds.Contains(p.LessonId))
            .ToDictionaryAsync(p => p.LessonId, p => p.CompletedAt, cancellationToken);

        var lessonProgressMap = new Dictionary<string, DateTime>();
        foreach (var chapter in currentPath.Chapters.OrderBy(c => c.OrderIndex))
        {
            foreach (var lesson in chapter.Lessons.OrderBy(l => l.OrderIndex))
            {
                if (oldLessonProgressByLessonId.TryGetValue(lesson.LessonId, out var completedAt))
                {
                    var key = BuildLessonKey(chapter.OrderIndex, chapter.Title, lesson.OrderIndex, lesson.Title);
                    lessonProgressMap[key] = completedAt;
                }
            }
        }

        var taskStatusMap = new Dictionary<string, (TaskStatus_ Status, DateTime? CompletedAt)>();
        foreach (var chapter in currentPath.Chapters.OrderBy(c => c.OrderIndex))
        {
            foreach (var task in chapter.Tasks)
            {
                var key = BuildTaskKey(chapter.OrderIndex, chapter.Title, task.TaskType, task.Title);
                if (!taskStatusMap.ContainsKey(key))
                {
                    taskStatusMap[key] = (task.Status, task.CompletedAt);
                }
            }
        }

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

        foreach (var chapter in currentPath.Chapters)
        {
            chapter.IsDeleted = true;
            chapter.DeletedAt = now;
            chapter.UpdatedAt = now;

            foreach (var lesson in chapter.Lessons)
            {
                lesson.IsDeleted = true;
                lesson.DeletedAt = now;
                lesson.UpdatedAt = now;
            }
        }

        foreach (var sourceChapter in sourcePath.Chapters.OrderBy(c => c.OrderIndex))
        {
            var newChapterId = NewId.NextGuid();
            await _context.Chapters.AddAsync(new Chapter
            {
                ChapterId = newChapterId,
                PathId = currentPath.PathId,
                Title = sourceChapter.Title,
                Content = sourceChapter.Content,
                OrderIndex = sourceChapter.OrderIndex,
                IsCompleted = false,
                StartDate = ShiftNullable(sourceChapter.StartDate),
                EndDate = ShiftNullable(sourceChapter.EndDate),
                EstimatedDays = sourceChapter.EstimatedDays,
                CreatedAt = now
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
                    CreatedAt = now
                }, cancellationToken);

                var lessonKey = BuildLessonKey(sourceChapter.OrderIndex, sourceChapter.Title, sourceLesson.OrderIndex, sourceLesson.Title);
                if (lessonProgressMap.TryGetValue(lessonKey, out var completedAt))
                {
                    await _context.LearnProgresses.AddAsync(new LearnProgress
                    {
                        ProgressId = NewId.NextGuid(),
                        LessonId = newLessonId,
                        UserId = studentId,
                        IsLessonContentRead = true,
                        CompletedAt = completedAt,
                        CreatedAt = now
                    }, cancellationToken);
                }

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
                        CreatedAt = now
                    }, cancellationToken);
                }
            }

            foreach (var sourceTask in sourceChapter.Tasks)
            {
                var taskKey = BuildTaskKey(sourceChapter.OrderIndex, sourceChapter.Title, sourceTask.TaskType, sourceTask.Title);
                var taskState = taskStatusMap.TryGetValue(taskKey, out var oldState)
                    ? oldState
                    : (sourceTask.Status, sourceTask.CompletedAt);

                await _context.Tasks.AddAsync(new TaskEntity
                {
                    TaskId = NewId.NextGuid(),
                    ChapterId = newChapterId,
                    PathId = currentPath.PathId,
                    Title = sourceTask.Title,
                    Description = sourceTask.Description,
                    DueDate = ShiftNullable(sourceTask.DueDate),
                    Priority = sourceTask.Priority,
                    Status = taskState.Status,
                    CreatedAt = now,
                    CompletedAt = taskState.CompletedAt,
                    TaskType = sourceTask.TaskType,
                    VerificationPrompt = sourceTask.VerificationPrompt,
                    MinimumScore = sourceTask.MinimumScore,
                    QuizQuestionsJson = sourceTask.QuizQuestionsJson
                }, cancellationToken);
            }
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

    private static string BuildLessonKey(int chapterOrder, string chapterTitle, int lessonOrder, string lessonTitle)
        => $"{chapterOrder}|{Normalize(chapterTitle)}|{lessonOrder}|{Normalize(lessonTitle)}";

    private static string BuildTaskKey(int chapterOrder, string chapterTitle, TaskType taskType, string taskTitle)
        => $"{chapterOrder}|{Normalize(chapterTitle)}|{taskType}|{Normalize(taskTitle)}";

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Join(' ', value.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
