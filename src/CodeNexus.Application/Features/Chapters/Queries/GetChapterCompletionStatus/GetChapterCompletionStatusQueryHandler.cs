using CodeNexus.Application.Common.Helpers;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Chapters.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Chapters.Queries.GetChapterCompletionStatus;

public class GetChapterCompletionStatusQueryHandler : IRequestHandler<GetChapterCompletionStatusQuery, Result<ChapterCompletionStatusDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetChapterCompletionStatusQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<ChapterCompletionStatusDto>> Handle(GetChapterCompletionStatusQuery request, CancellationToken cancellationToken)
    {
        Guid userId;
        try
        {
            userId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<ChapterCompletionStatusDto>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        var chapterInfo = await _context.Chapters
            .AsNoTracking()
            .Where(c => c.ChapterId == request.ChapterId && !c.IsDeleted)
            .Select(c => new
            {
                c.ChapterId,
                c.PathId,
                OwnerId = c.LearningPath.UserId,
                c.IsCompleted
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (chapterInfo == null)
        {
            return Result<ChapterCompletionStatusDto>.Failure("CHAPTER_NOT_FOUND", "Chapter not found.");
        }

        if (chapterInfo.OwnerId != userId)
        {
            return Result<ChapterCompletionStatusDto>.Failure("ACCESS_DENIED", "Access denied.");
        }

        var totalLessons = await _context.Lessons
            .AsNoTracking()
            .CountAsync(l => l.ChapterId == request.ChapterId && !l.IsDeleted, cancellationToken);

        var completedLessons = await _context.LearnProgresses
            .AsNoTracking()
            .Where(p =>
                p.UserId == userId &&
                p.IsLessonContentRead &&
                !p.Lesson.IsDeleted &&
                p.Lesson.ChapterId == request.ChapterId)
            .Select(p => p.LessonId)
            .Distinct()
            .CountAsync(cancellationToken);

        var totalTasks = await _context.Tasks
            .AsNoTracking()
            .CountAsync(t => t.ChapterId == request.ChapterId, cancellationToken);

        var completedTasks = await _context.Tasks
            .AsNoTracking()
            .CountAsync(t => t.ChapterId == request.ChapterId && t.Status == Domain.Enums.TaskStatus_.Completed, cancellationToken);

        var totalQuizzes = await _context.Quizzes
            .AsNoTracking()
            .CountAsync(q =>
                !q.IsDeleted &&
                q.LessonId.HasValue &&
                !q.Lesson!.IsDeleted &&
                q.Lesson.ChapterId == request.ChapterId,
                cancellationToken);

        var completedQuizzes = await _context.QuizAttempts
            .AsNoTracking()
            .Where(a =>
                a.UserId == userId &&
                a.Status == Domain.Enums.QuizAttemptStatus.Passed &&
                !a.Quiz.IsDeleted &&
                a.Quiz.LessonId.HasValue &&
                !a.Quiz.Lesson!.IsDeleted &&
                a.Quiz.Lesson.ChapterId == request.ChapterId)
            .Select(a => a.QuizId)
            .Distinct()
            .CountAsync(cancellationToken);

        var totalItems = totalLessons + totalTasks + totalQuizzes;
        var completedItems = completedLessons + completedTasks + completedQuizzes;
        var progressPercent = totalItems == 0
            ? 0m
            : Math.Round(completedItems * 100m / totalItems, 2);

        var isCompleted = totalItems > 0
            && completedLessons == totalLessons
            && completedTasks == totalTasks
            && completedQuizzes == totalQuizzes;

        if (chapterInfo.IsCompleted != isCompleted)
        {
            var hasChapterCompletionChanges = await ChapterCompletionSyncHelper.SyncAsync(
                _context,
                request.ChapterId,
                userId,
                cancellationToken);

            if (hasChapterCompletionChanges)
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        var dto = new ChapterCompletionStatusDto(
            request.ChapterId,
            isCompleted,
            completedLessons,
            totalLessons,
            completedTasks,
            totalTasks,
            completedQuizzes,
            totalQuizzes,
            progressPercent);

        return Result<ChapterCompletionStatusDto>.Success(dto);
    }
}
