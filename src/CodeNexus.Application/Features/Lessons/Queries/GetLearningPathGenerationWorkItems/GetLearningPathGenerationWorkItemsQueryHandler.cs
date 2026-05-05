using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Lessons.Queries.GetLearningPathGenerationWorkItems;

public class GetLearningPathGenerationWorkItemsQueryHandler
    : IRequestHandler<GetLearningPathGenerationWorkItemsQuery, Result<LearningPathGenerationWorkItemsDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetLearningPathGenerationWorkItemsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<LearningPathGenerationWorkItemsDto>> Handle(
        GetLearningPathGenerationWorkItemsQuery request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();

        var path = await _context.LearningPaths
            .AsNoTracking()
            .Where(lp => lp.PathId == request.PathId)
            .Select(lp => new { lp.PathId, lp.UserId })
            .FirstOrDefaultAsync(cancellationToken);

        if (path == null)
        {
            return Result<LearningPathGenerationWorkItemsDto>.Failure(
                "LEARNING_PATH_NOT_FOUND",
                "Learning path not found.");
        }

        if (path.UserId != userId)
        {
            return Result<LearningPathGenerationWorkItemsDto>.Failure(
                "UNAUTHORIZED",
                "User not authenticated");
        }

        var lessons = await _context.Lessons
            .AsNoTracking()
            .Where(l => !l.IsDeleted
                        && !l.Chapter.IsDeleted
                        && l.Chapter.PathId == request.PathId)
            .Select(l => new
            {
                l.LessonId,
                l.UpdatedAt,
                l.Content
            })
            .ToListAsync(cancellationToken);

        var pendingLessonIds = lessons
            .Where(l => l.UpdatedAt == null || string.IsNullOrWhiteSpace(l.Content))
            .Select(l => l.LessonId)
            .Distinct()
            .ToList();

        var quizzes = await _context.Quizzes
            .AsNoTracking()
            .Where(q => !q.IsDeleted
                        && q.LessonId.HasValue
                        && q.Lesson != null
                        && !q.Lesson.IsDeleted
                        && !q.Lesson.Chapter.IsDeleted
                        && q.Lesson.Chapter.PathId == request.PathId)
            .Select(q => new
            {
                q.QuizId,
                LessonId = q.LessonId!.Value
            })
            .ToListAsync(cancellationToken);

        var quizIds = quizzes.Select(q => q.QuizId).ToList();
        var quizIdsWithQuestions = quizIds.Count == 0
            ? new HashSet<Guid>()
            : (await _context.Questions
                .AsNoTracking()
                .Where(q => !q.IsDeleted && quizIds.Contains(q.QuizId))
                .Select(q => q.QuizId)
                .Distinct()
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var pendingQuizzesByLesson = quizzes
            .Where(q => !quizIdsWithQuestions.Contains(q.QuizId))
            .GroupBy(q => q.LessonId)
            .Select(g => new LessonPendingQuizDto(
                g.Key,
                g.Select(x => x.QuizId).Distinct().ToList()))
            .ToList();

        return Result<LearningPathGenerationWorkItemsDto>.Success(
            new LearningPathGenerationWorkItemsDto(
                request.PathId,
                pendingLessonIds,
                pendingQuizzesByLesson));
    }
}

