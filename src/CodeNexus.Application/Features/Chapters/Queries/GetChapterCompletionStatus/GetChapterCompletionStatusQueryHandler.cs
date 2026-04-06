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
		var userId = _currentUserService.GetUserId();

		var chapter = await _context.Chapters
				.Include(c => c.LearningPath)
					.ThenInclude(lp => lp.Subject)
				.Include(c => c.Lessons)
			.FirstOrDefaultAsync(c => c.ChapterId == request.ChapterId, cancellationToken);

		if (chapter == null)
			return Result<ChapterCompletionStatusDto>.Failure("CHAPTER_NOT_FOUND", "Chapter not found");

		if (chapter.LearningPath.UserId != userId)
			return Result<ChapterCompletionStatusDto>.Failure("UNAUTHORIZED", "User not authenticated");


		var totalTasks = await _context.Tasks
            .AsNoTracking()
            .CountAsync(t => t.ChapterId == request.ChapterId, cancellationToken);

        var completedTasks = await _context.Tasks
            .AsNoTracking()
            .CountAsync(t => t.ChapterId == request.ChapterId && t.Status == Domain.Enums.TaskStatus_.Completed, cancellationToken);

        var lessonIds = chapter.Lessons
            .Where(l => !l.IsDeleted)
            .Select(l => l.LessonId)
            .ToList();

        var totalQuizzes = await _context.Quizzes
            .AsNoTracking()
            .CountAsync(q =>
                !q.IsDeleted &&
                q.LessonId.HasValue &&
                lessonIds.Contains(q.LessonId.Value),
                cancellationToken);

        var completedQuizzes = await _context.QuizAttempts
            .AsNoTracking()
            .Where(a =>
                a.UserId == userId &&
                a.Status == Domain.Enums.QuizAttemptStatus.Passed &&
                !a.Quiz.IsDeleted &&
                a.Quiz.LessonId.HasValue &&
                lessonIds.Contains(a.Quiz.LessonId.Value))
            .Select(a => a.QuizId)
            .Distinct()
            .CountAsync(cancellationToken);

        var totalItems = totalTasks + totalQuizzes;

        var shouldMarkCompleted = totalItems > 0
            && completedTasks == totalTasks
            && completedQuizzes == totalQuizzes;

        if (!chapter.IsCompleted && shouldMarkCompleted)
        {
            chapter.IsCompleted = true;
            await _context.SaveChangesAsync(cancellationToken);
        }

        var dto = new ChapterCompletionStatusDto(chapter.IsCompleted);

        return Result<ChapterCompletionStatusDto>.Success(dto);
    }
}
