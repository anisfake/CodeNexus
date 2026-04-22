using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Queries.GetPublishedLearningPathPreview;

public class GetPublishedLearningPathPreviewQueryHandler : IRequestHandler<GetPublishedLearningPathPreviewQuery, Result<PublishedLearningPathPreviewDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetPublishedLearningPathPreviewQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PublishedLearningPathPreviewDto>> Handle(GetPublishedLearningPathPreviewQuery request, CancellationToken cancellationToken)
    {
        Guid studentId;
        try
        {
            studentId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<PublishedLearningPathPreviewDto>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        var learningPath = await _context.LearningPaths
            .AsNoTracking()
            .Include(lp => lp.Subject)
            .Include(lp => lp.User)
            .Include(lp => lp.LearningPathGoals)
                .ThenInclude(lpg => lpg.Goal)
            .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                .ThenInclude(c => c.Lessons.Where(l => !l.IsDeleted))
                    .ThenInclude(l => l.Quizzes.Where(q => !q.IsDeleted))
            .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                .ThenInclude(c => c.Tasks.Where(t => !t.IsDeleted))
            .FirstOrDefaultAsync(lp => lp.PathId == request.PathId, cancellationToken);

        if (learningPath == null)
        {
            return Result<PublishedLearningPathPreviewDto>.Failure("LEARNING_PATH_NOT_FOUND", "Learning path not found.");
        }

        if (learningPath.Status != LearningPathStatus.Published.ToString())
        {
            return Result<PublishedLearningPathPreviewDto>.Failure("LEARNING_PATH_NOT_PUBLISHED", "Learning path is not published.");
        }

        var isEnrolled = await _context.LearningPathShares
            .AsNoTracking()
            .AnyAsync(s => s.PathId == request.PathId
                           && s.StudentId == studentId
                           && s.Status == LearningPathShareStatus.Accepted,
                      cancellationToken);

        var goals = learningPath.LearningPathGoals
            .OrderByDescending(g => g.Weight)
            .Select(g => new LearningPathGoalDto(
                g.GoalId,
                g.Goal?.Title ?? string.Empty,
                g.Weight,
                g.Goal?.DurationInDays ?? 0,
                "NotStarted",
                null,
                0m,
                g.Weight * 100m
            )).ToList();

        var chapters = learningPath.Chapters
            .OrderBy(c => c.OrderIndex)
            .Select(c => new ChapterPreviewDto(
                c.ChapterId,
                c.Title,
                c.Content,
                c.OrderIndex,
                c.Lessons
                    .OrderBy(l => l.LessonDay)
                    .Select(l => new LessonPreviewDto(
                        l.LessonId,
                        l.Title,
                        l.LessonDay,
                        l.Quizzes.Count(q => !q.IsDeleted)
                    )).ToList(),
                c.Tasks
                    .Select(t => new TaskPreviewDto(
                        t.TaskId,
                        t.Title,
                        t.TaskType,
                        t.Priority,
                        t.DueDate
                    )).ToList()
            )).ToList();

        var totalLessons = chapters.Sum(c => c.Lessons.Count);

        var dto = new PublishedLearningPathPreviewDto(
            learningPath.PathId,
            learningPath.Title,
            learningPath.Description,
            learningPath.SubjectId,
            learningPath.Subject?.Name ?? string.Empty,
            learningPath.ComplexityLevel,
            learningPath.Language,
            learningPath.VersionNumber,
            learningPath.UserId,
            learningPath.User?.Username ?? string.Empty,
            learningPath.StartDate,
            learningPath.EndDate,
            goals,
            chapters,
            chapters.Count,
            totalLessons,
            isEnrolled
        );

        return Result<PublishedLearningPathPreviewDto>.Success(dto);
    }
}
