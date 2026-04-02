using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPathShares.DTOs;
using CodeNexus.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPathShares.Commands.AcceptLearningPathShare;

public class AcceptLearningPathShareCommandHandler : IRequestHandler<AcceptLearningPathShareCommand, Result<LearningPathShareDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public AcceptLearningPathShareCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<LearningPathShareDto>> Handle(AcceptLearningPathShareCommand request, CancellationToken cancellationToken)
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

        var student = await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == studentId, cancellationToken);

        if (student == null)
        {
            return Result<LearningPathShareDto>.Failure("USER_NOT_FOUND", "User not found.");
        }

        if (!string.Equals(student.Role?.RoleName, "Student", StringComparison.OrdinalIgnoreCase))
        {
            return Result<LearningPathShareDto>.Failure("ACCESS_DENIED", "Access denied.");
        }

        var share = await _context.LearningPathShares
            .FirstOrDefaultAsync(s => s.ShareId == request.ShareId && s.StudentId == studentId, cancellationToken);

        if (share == null)
        {
            return Result<LearningPathShareDto>.Failure("SHARE_NOT_FOUND", "Learning path share not found.");
        }

        if (share.Status != LearningPathShareStatus.Pending)
        {
            return Result<LearningPathShareDto>.Failure("INVALID_SHARE_STATE", "Only pending shares can be processed.");
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
            return Result<LearningPathShareDto>.Failure("LEARNING_PATH_NOT_FOUND", "Learning path not found.");
        }

        var acceptedAt = DateTime.UtcNow.AddHours(7);
        var timelineAnchor = ResolveTimelineAnchor(sourcePath) ?? acceptedAt;
        var timelineShift = acceptedAt - timelineAnchor;

        DateTime? ShiftNullable(DateTime? value)
            => value.HasValue ? value.Value.Add(timelineShift) : null;

        DateTime Shift(DateTime value)
            => value.Add(timelineShift);

        var studentPathId = NewId.NextGuid();
        var studentPath = new Domain.Entities.LearningPath
        {
            PathId = studentPathId,
            UserId = studentId,
            SubjectId = sourcePath.SubjectId,
            Title = sourcePath.Title,
            Description = sourcePath.Description,
            StartDate = acceptedAt,
            EndDate = ShiftNullable(sourcePath.EndDate),
            Status = LearningPathStatus.Active.ToString(),
            CreatedAt = acceptedAt,
            CreatedByType = sourcePath.CreatedByType,
            Language = sourcePath.Language,
            ComplexityLevel = sourcePath.ComplexityLevel
        };

        await _context.LearningPaths.AddAsync(studentPath, cancellationToken);

        foreach (var goal in sourcePath.LearningPathGoals)
        {
            await _context.LearningPathGoals.AddAsync(new Domain.Entities.LearningPathGoal
            {
                PathId = studentPathId,
                GoalId = goal.GoalId,
                Weight = goal.Weight
            }, cancellationToken);
        }

        foreach (var sourceChapter in sourcePath.Chapters.OrderBy(c => c.OrderIndex))
        {
            var newChapterId = NewId.NextGuid();
            await _context.Chapters.AddAsync(new Domain.Entities.Chapter
            {
                ChapterId = newChapterId,
                PathId = studentPathId,
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
                await _context.Lessons.AddAsync(new Domain.Entities.Lesson
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
                    await _context.Quizzes.AddAsync(new Domain.Entities.Quiz
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
                await _context.Tasks.AddAsync(new Domain.Entities.Tasks
                {
                    TaskId = NewId.NextGuid(),
                    ChapterId = newChapterId,
                    PathId = studentPathId,
                    Title = sourceTask.Title,
                    Description = sourceTask.Description,
                    DueDate = ShiftNullable(sourceTask.DueDate),
                    Priority = sourceTask.Priority,
                    Status = sourceTask.Status,
                    CreatedAt = acceptedAt,
                    TaskType = sourceTask.TaskType,
                    VerificationPrompt = sourceTask.VerificationPrompt,
                    MinimumScore = sourceTask.MinimumScore,
                    QuizQuestionsJson = sourceTask.QuizQuestionsJson
                }, cancellationToken);
            }
        }

        share.Status = LearningPathShareStatus.Accepted;
        share.RespondedAt = acceptedAt;

        await _context.SaveChangesAsync(cancellationToken);

        return Result<LearningPathShareDto>.Success(new LearningPathShareDto(
            share.ShareId,
            share.PathId,
            share.MentorId,
            share.StudentId,
            share.Status,
            share.SentAt,
            share.RespondedAt
        ));
    }

    private static DateTime? ResolveTimelineAnchor(Domain.Entities.LearningPath sourcePath)
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
