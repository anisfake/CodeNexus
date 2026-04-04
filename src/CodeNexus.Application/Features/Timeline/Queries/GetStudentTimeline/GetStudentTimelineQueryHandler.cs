using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Timeline.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Timeline.Queries.GetStudentTimeline;

public class GetStudentTimelineQueryHandler : IRequestHandler<GetStudentTimelineQuery, Result<StudentTimelineResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetStudentTimelineQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<StudentTimelineResponse>> Handle(GetStudentTimelineQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();
        var fromUtc = request.FromUtc ?? DateTime.UtcNow.Date;
        var toUtc = request.ToUtc ?? fromUtc.AddDays(7).AddTicks(-1);

        if (fromUtc > toUtc)
        {
            return Result<StudentTimelineResponse>.Failure("INVALID_DATE_RANGE", "FromUtc must be earlier than or equal to ToUtc.");
        }

        var pathQuery = _context.LearningPaths
            .AsNoTracking()
            .Where(lp => lp.UserId == userId);

        if (request.OnlyActivePaths)
        {
            var active = LearningPathStatus.Active.ToString();
            var inProgress = LearningPathStatus.InProgress.ToString();
            pathQuery = pathQuery.Where(lp => lp.Status == active || lp.Status == inProgress);
        }

        if (request.LearningPathId.HasValue)
        {
            pathQuery = pathQuery.Where(lp => lp.PathId == request.LearningPathId.Value);
        }

        var paths = await pathQuery
            .Select(lp => new { lp.PathId, lp.Title })
            .ToListAsync(cancellationToken);

        if (request.LearningPathId.HasValue && paths.Count == 0)
        {
            return Result<StudentTimelineResponse>.Failure("LEARNING_PATH_NOT_FOUND", "Learning path not found.");
        }

        if (paths.Count == 0)
        {
            return Result<StudentTimelineResponse>.Success(new StudentTimelineResponse(
                fromUtc,
                toUtc,
                0,
                0,
                new List<StudentTimelineItemDto>()));
        }

        var pathIds = paths.Select(x => x.PathId).ToList();
        var pathTitleById = paths.ToDictionary(x => x.PathId, x => x.Title);

        var lessonRows = await _context.Lessons
            .AsNoTracking()
            .Where(l =>
                !l.IsDeleted
                && pathIds.Contains(l.Chapter.PathId)
                && l.LessonDay >= fromUtc
                && l.LessonDay <= toUtc)
            .Select(l => new
            {
                l.LessonId,
                l.Title,
                l.LessonDay,
                l.ChapterId,
                ChapterTitle = l.Chapter.Title,
                PathId = l.Chapter.PathId
            })
            .ToListAsync(cancellationToken);

        var lessonIds = lessonRows.Select(x => x.LessonId).ToList();

        var completedLessonIds = lessonIds.Count == 0
            ? new HashSet<Guid>()
            : (await _context.LearnProgresses
                .AsNoTracking()
                .Where(p => p.UserId == userId && lessonIds.Contains(p.LessonId))
                .Select(p => p.LessonId)
                .Distinct()
                .ToListAsync(cancellationToken)).ToHashSet();

        var taskRows = await _context.Tasks
            .AsNoTracking()
            .Where(t =>
                t.DueDate.HasValue
                && pathIds.Contains(t.PathId)
                && t.DueDate.Value >= fromUtc
                && t.DueDate.Value <= toUtc)
            .Select(t => new
            {
                t.TaskId,
                t.Title,
                DueAt = t.DueDate!.Value,
                t.PathId,
                t.ChapterId,
                ChapterTitle = t.Chapter.Title,
                t.Priority,
                t.Status
            })
            .ToListAsync(cancellationToken);

        var quizRows = await _context.Quizzes
            .AsNoTracking()
            .Where(q =>
                !q.IsDeleted
                && q.LessonId.HasValue
                && pathIds.Contains(q.Lesson!.Chapter.PathId)
                && (q.DueDate ?? q.Lesson!.LessonDay) >= fromUtc
                && (q.DueDate ?? q.Lesson!.LessonDay) <= toUtc)
            .Select(q => new
            {
                q.QuizId,
                q.Title,
                DueAt = q.DueDate ?? q.Lesson!.LessonDay,
                PathId = q.Lesson!.Chapter.PathId,
                ChapterId = q.Lesson!.ChapterId,
                ChapterTitle = q.Lesson!.Chapter.Title,
                q.LessonId
            })
            .ToListAsync(cancellationToken);

        var quizIds = quizRows.Select(x => x.QuizId).ToList();
        var passedQuizIds = quizIds.Count == 0
            ? new HashSet<Guid>()
            : (await _context.QuizAttempts
                .AsNoTracking()
                .Where(a => a.UserId == userId && quizIds.Contains(a.QuizId) && a.Status == QuizAttemptStatus.Passed)
                .Select(a => a.QuizId)
                .Distinct()
                .ToListAsync(cancellationToken)).ToHashSet();

        var nowUtc = DateTime.UtcNow;
        var items = new List<StudentTimelineItemDto>();

        items.AddRange(lessonRows.Select(l =>
        {
            var isCompleted = completedLessonIds.Contains(l.LessonId);
            return new StudentTimelineItemDto(
                l.LessonId,
                "Lesson",
                l.Title,
                l.LessonDay,
                l.PathId,
                pathTitleById.GetValueOrDefault(l.PathId, string.Empty),
                l.ChapterId,
                l.ChapterTitle,
                l.LessonId,
                isCompleted ? "Completed" : "Pending",
                isCompleted,
                !isCompleted && l.LessonDay < nowUtc,
                null);
        }));

        items.AddRange(taskRows.Select(t =>
        {
            var isCompleted = t.Status == TaskStatus_.Completed;
            return new StudentTimelineItemDto(
                t.TaskId,
                "Task",
                t.Title,
                t.DueAt,
                t.PathId,
                pathTitleById.GetValueOrDefault(t.PathId, string.Empty),
                t.ChapterId,
                t.ChapterTitle,
                null,
                t.Status.ToString(),
                isCompleted,
                !isCompleted && t.DueAt < nowUtc,
                t.Priority.HasValue ? (int)t.Priority.Value : null);
        }));

        items.AddRange(quizRows.Select(q =>
        {
            var isCompleted = passedQuizIds.Contains(q.QuizId);
            return new StudentTimelineItemDto(
                q.QuizId,
                "Quiz",
                q.Title,
                q.DueAt,
                q.PathId,
                pathTitleById.GetValueOrDefault(q.PathId, string.Empty),
                q.ChapterId,
                q.ChapterTitle,
                q.LessonId,
                isCompleted ? "Passed" : "Pending",
                isCompleted,
                !isCompleted && q.DueAt < nowUtc,
                null);
        }));

        var orderedItems = items
            .OrderBy(x => x.DueAtUtc)
            .ThenBy(x => x.ItemType)
            .ThenBy(x => x.Title)
            .ToList();

        var response = new StudentTimelineResponse(
            fromUtc,
            toUtc,
            orderedItems.Count,
            orderedItems.Count(x => x.IsOverdue),
            orderedItems);

        return Result<StudentTimelineResponse>.Success(response);
    }
}

