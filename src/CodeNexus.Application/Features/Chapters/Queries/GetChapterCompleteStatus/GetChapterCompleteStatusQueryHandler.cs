using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Chapters.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CodeNexus.Application.Features.Chapters.Queries.GetChapterCompleteStatus;

public class GetChapterCompleteStatusQueryHandler : IRequestHandler<GetChapterCompleteStatusQuery, Result<ChapterCompleteStatusDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetChapterCompleteStatusQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<ChapterCompleteStatusDto>> Handle(GetChapterCompleteStatusQuery request, CancellationToken cancellationToken)
    {
        Guid userId;
        try
        {
            userId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<ChapterCompleteStatusDto>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        var chapter = await _context.Chapters
				.Include(c => c.LearningPath)
					.ThenInclude(lp => lp.Subject)
				.Include(c => c.Lessons)
			.FirstOrDefaultAsync(c => c.ChapterId == request.ChapterId, cancellationToken);

		if (chapter == null)
        {
            return Result<ChapterCompleteStatusDto>.Failure("CHAPTER_NOT_FOUND", "Chapter not found.");
        }

        if (chapter.LearningPath.UserId != userId)
        {
            return Result<ChapterCompleteStatusDto>.Failure("ACCESS_DENIED", "Access denied.");
        }

        var lessonIds = await _context.Lessons
            .AsNoTracking()
            .Where(l => l.ChapterId == request.ChapterId && !l.IsDeleted)
            .Select(l => l.LessonId)
            .ToListAsync(cancellationToken);

        var quizzes = await _context.Quizzes
            .AsNoTracking()
            .Where(q => q.LessonId.HasValue && lessonIds.Contains(q.LessonId.Value) && !q.IsDeleted)
            .Select(q => q.QuizId)
            .ToListAsync(cancellationToken);

        var totalQuizzes = quizzes.Count;
        var passedQuizzes = 0;

        if (totalQuizzes > 0)
        {
            passedQuizzes = await _context.QuizAttempts
                .AsNoTracking()
                .Where(a => quizzes.Contains(a.QuizId) && a.UserId == userId && a.Status == QuizAttemptStatus.Passed)
                .Select(a => a.QuizId)
                .Distinct()
                .CountAsync(cancellationToken);
        }

        var totalTasks = await _context.Tasks
            .AsNoTracking()
            .Where(t => t.ChapterId == request.ChapterId)
            .CountAsync(cancellationToken);

        var completedTasks = await _context.Tasks
            .AsNoTracking()
            .Where(t => t.ChapterId == request.ChapterId && t.Status == TaskStatus_.Completed)
            .CountAsync(cancellationToken);

        bool allQuizzesPassed = totalQuizzes == 0 || (passedQuizzes == totalQuizzes);
        bool allTasksCompleted = totalTasks == 0 || (completedTasks == totalTasks);
        bool isCompleted = allQuizzesPassed && allTasksCompleted;

        if (isCompleted && !chapter.IsCompleted)
        {
            chapter.IsCompleted = true;
            await _context.SaveChangesAsync(cancellationToken);
        }

        var dto = new ChapterCompleteStatusDto(isCompleted);

        return Result<ChapterCompleteStatusDto>.Success(dto);
    }
}
