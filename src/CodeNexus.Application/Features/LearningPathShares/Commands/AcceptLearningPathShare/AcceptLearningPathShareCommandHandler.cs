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
            return Result<LearningPathShareDto>.Failure("ACCESS_DENIED", "Only students can accept a learning path share.");
        }

        var share = await _context.LearningPathShares
            .FirstOrDefaultAsync(s => s.ShareId == request.ShareId && s.StudentId == studentId, cancellationToken);

        if (share == null)
        {
            return Result<LearningPathShareDto>.Failure("SHARE_NOT_FOUND", "Learning path share not found.");
        }

        if (share.Status != LearningPathShareStatus.Pending)
        {
            return Result<LearningPathShareDto>.Failure("INVALID_SHARE_STATE", "Only pending shares can be accepted.");
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

        var studentPathId = NewId.NextGuid();
        var studentPath = new Domain.Entities.LearningPath
        {
            PathId = studentPathId,
            UserId = studentId,
            SubjectId = sourcePath.SubjectId,
            Title = sourcePath.Title,
            Description = sourcePath.Description,
            StartDate = sourcePath.StartDate,
            EndDate = sourcePath.EndDate,
            Status = LearningPathStatus.Active.ToString(),
            CreatedAt = DateTime.UtcNow,
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
                StartDate = sourceChapter.StartDate,
                EndDate = sourceChapter.EndDate,
                EstimatedDays = sourceChapter.EstimatedDays,
                CreatedAt = DateTime.UtcNow
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
                    LessonDay = sourceLesson.LessonDay,
                    CreatedAt = DateTime.UtcNow
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
                        DueDate = sourceQuiz.DueDate,
                        CreatedAt = DateTime.UtcNow
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
                    DueDate = sourceTask.DueDate,
                    Priority = sourceTask.Priority,
                    Status = sourceTask.Status,
                    CreatedAt = DateTime.UtcNow,
                    TaskType = sourceTask.TaskType,
                    VerificationPrompt = sourceTask.VerificationPrompt,
                    MinimumScore = sourceTask.MinimumScore,
                    QuizQuestionsJson = sourceTask.QuizQuestionsJson
                }, cancellationToken);
            }
        }

        share.Status = LearningPathShareStatus.Accepted;
        share.RespondedAt = DateTime.UtcNow;

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
}
