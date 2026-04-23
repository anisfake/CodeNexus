using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPaths.Queries.GetLearningPathProgress;

public class GetLearningPathProgressQueryHandler : IRequestHandler<GetLearningPathProgressQuery, Result<LearningPathCompletionProgressDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetLearningPathProgressQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<LearningPathCompletionProgressDto>> Handle(GetLearningPathProgressQuery request, CancellationToken cancellationToken)
    {
        Guid userId;
        try
        {
            userId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<LearningPathCompletionProgressDto>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        var learningPathOwnerId = await _context.LearningPaths
            .AsNoTracking()
            .Where(lp => lp.PathId == request.PathId)
            .Select(lp => (Guid?)lp.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!learningPathOwnerId.HasValue)
            return Result<LearningPathCompletionProgressDto>.Failure("LEARNING_PATH_NOT_FOUND", "Learning path not found.");

        bool isMentorReviewPath = await _context.LearningPathMentorReviews
            .AsNoTracking()
            .AnyAsync(r => r.RevisedPathId == request.PathId && r.StudentId == userId, cancellationToken);

        if (learningPathOwnerId.Value != userId && !isMentorReviewPath)
            return Result<LearningPathCompletionProgressDto>.Failure("ACCESS_DENIED", "Access denied.");

        var totalLessonContents = await _context.Lessons
            .AsNoTracking()
            .CountAsync(l =>
                !l.IsDeleted &&
                !l.Chapter.IsDeleted &&
                l.Chapter.PathId == request.PathId,
                cancellationToken);

        var completedLessonContents = await _context.LearnProgresses
            .AsNoTracking()
            .CountAsync(p =>
                p.UserId == userId &&
                p.IsLessonContentRead &&
                !p.Lesson.IsDeleted &&
                !p.Lesson.Chapter.IsDeleted &&
                p.Lesson.Chapter.PathId == request.PathId,
                cancellationToken);

        var totalQuizzes = await _context.Quizzes
            .AsNoTracking()
            .CountAsync(q =>
                !q.IsDeleted &&
                q.LessonId.HasValue &&
                !q.Lesson!.IsDeleted &&
                !q.Lesson.Chapter.IsDeleted &&
                q.Lesson.Chapter.PathId == request.PathId,
                cancellationToken);

        var completedQuizzes = await (from attempt in _context.QuizAttempts.AsNoTracking()
                                      join quiz in _context.Quizzes.AsNoTracking() on attempt.QuizId equals quiz.QuizId
                                      where attempt.UserId == userId
                                            && attempt.Status == QuizAttemptStatus.Passed
                                            && !quiz.IsDeleted
                                            && quiz.LessonId.HasValue
                                            && !quiz.Lesson!.IsDeleted
                                            && !quiz.Lesson.Chapter.IsDeleted
                                            && quiz.Lesson.Chapter.PathId == request.PathId
                                      select attempt.QuizId)
            .Distinct()
            .CountAsync(cancellationToken);

        var totalTasks = await _context.Tasks
            .AsNoTracking()
            .CountAsync(t =>
                t.PathId == request.PathId &&
                !t.Chapter.IsDeleted,
                cancellationToken);

        var completedTasks = await _context.Tasks
            .AsNoTracking()
            .CountAsync(t =>
                t.PathId == request.PathId &&
                !t.Chapter.IsDeleted &&
                t.Status == TaskStatus_.Completed,
                cancellationToken);

        var totalItems = totalLessonContents + totalQuizzes + totalTasks;
        var completedItems = completedLessonContents + completedQuizzes + completedTasks;

        var contentProgressPercent = totalLessonContents == 0
            ? 0m
            : Math.Round(completedLessonContents * 100m / totalLessonContents, 2);

        var quizProgressPercent = totalQuizzes == 0
            ? 0m
            : Math.Round(completedQuizzes * 100m / totalQuizzes, 2);

        var rawPercent = totalItems == 0
            ? 0m
            : Math.Round(completedItems * 100m / totalItems, 2);

        var progressPercent = Math.Min(100m, rawPercent);

        var status = completedItems switch
        {
            0 => "NotStarted",
            _ when totalItems > 0 && completedItems >= totalItems => "Completed",
            _ => "InProgress"
        };

        var dto = new LearningPathCompletionProgressDto(
            request.PathId,
            completedLessonContents,
            totalLessonContents,
            contentProgressPercent,
            completedQuizzes,
            totalQuizzes,
            quizProgressPercent,
            completedTasks,
            totalTasks,
            progressPercent,
            status
        );

        return Result<LearningPathCompletionProgressDto>.Success(dto);
    }
}