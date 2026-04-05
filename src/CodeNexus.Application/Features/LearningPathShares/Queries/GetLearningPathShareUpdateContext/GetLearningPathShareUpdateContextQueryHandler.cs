using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPathShares.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPathShares.Queries.GetLearningPathShareUpdateContext;

public class GetLearningPathShareUpdateContextQueryHandler
    : IRequestHandler<GetLearningPathShareUpdateContextQuery, Result<LearningPathShareUpdateContextDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetLearningPathShareUpdateContextQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<LearningPathShareUpdateContextDto>> Handle(GetLearningPathShareUpdateContextQuery request, CancellationToken cancellationToken)
    {
        Guid studentId;
        try
        {
            studentId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<LearningPathShareUpdateContextDto>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        var share = await _context.LearningPathShares
            .AsNoTracking()
            .Include(s => s.Mentor)
            .Include(s => s.LearningPath)
            .FirstOrDefaultAsync(s => s.ShareId == request.ShareId && s.StudentId == studentId, cancellationToken);

        if (share == null)
        {
            return Result<LearningPathShareUpdateContextDto>.Failure("SHARE_NOT_FOUND", "Learning path share not found.");
        }

        if (share.Status != LearningPathShareStatus.Accepted)
        {
            return Result<LearningPathShareUpdateContextDto>.Failure("INVALID_SHARE_STATE", "Only accepted shares are supported.");
        }

        var sourcePath = await _context.LearningPaths
            .AsNoTracking()
            .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                .ThenInclude(c => c.Lessons.Where(l => !l.IsDeleted))
            .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                .ThenInclude(c => c.Tasks)
            .FirstOrDefaultAsync(lp => lp.PathId == share.PathId, cancellationToken);

        if (sourcePath == null)
        {
            return Result<LearningPathShareUpdateContextDto>.Failure("SOURCE_LEARNING_PATH_NOT_FOUND", "Source learning path not found.");
        }

        LearningPath? acceptedPath = null;
        if (share.AcceptedPathId.HasValue)
        {
            acceptedPath = await _context.LearningPaths
                .AsNoTracking()
                .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                    .ThenInclude(c => c.Lessons.Where(l => !l.IsDeleted))
                .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                    .ThenInclude(c => c.Tasks)
                .FirstOrDefaultAsync(
                    lp => lp.PathId == share.AcceptedPathId.Value && lp.UserId == studentId,
                    cancellationToken);
        }

        var currentVersion = share.SourceVersionAtAccept ?? 1;
        var latestVersion = sourcePath.VersionNumber;
        var hasNewVersion = latestVersion > currentVersion
            && (!share.IgnoredSourceVersion.HasValue || share.IgnoredSourceVersion.Value < latestVersion);

        var changeSummary = BuildChangeSummary(acceptedPath, sourcePath);

        return Result<LearningPathShareUpdateContextDto>.Success(new LearningPathShareUpdateContextDto(
            share.ShareId,
            share.PathId,
            sourcePath.Title,
            share.AcceptedPathId,
            share.MentorId,
            share.Mentor.Username,
            currentVersion,
            latestVersion,
            hasNewVersion,
            share.IgnoredSourceVersion,
            share.LastNotifiedSourceVersion,
            changeSummary
        ));
    }

    private static LearningPathShareUpdateChangeSummaryDto? BuildChangeSummary(LearningPath? currentPath, LearningPath latestSourcePath)
    {
        if (currentPath == null)
        {
            return null;
        }

        var sourceChapters = latestSourcePath.Chapters
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.OrderIndex)
            .ToList();

        var currentChapters = currentPath.Chapters
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.OrderIndex)
            .ToList();

        var sourceChapterByOrder = sourceChapters
            .GroupBy(c => c.OrderIndex)
            .ToDictionary(g => g.Key, g => g.First());
        var currentChapterByOrder = currentChapters
            .GroupBy(c => c.OrderIndex)
            .ToDictionary(g => g.Key, g => g.First());

        var addedChapters = sourceChapterByOrder.Keys
            .Except(currentChapterByOrder.Keys)
            .Select(order => sourceChapterByOrder[order].Title)
            .ToList();

        var removedChapters = currentChapterByOrder.Keys
            .Except(sourceChapterByOrder.Keys)
            .Select(order => currentChapterByOrder[order].Title)
            .ToList();

        var updatedChapters = sourceChapterByOrder.Keys
            .Intersect(currentChapterByOrder.Keys)
            .Where(order => !TextEquals(sourceChapterByOrder[order].Title, currentChapterByOrder[order].Title)
                            || !TextEquals(sourceChapterByOrder[order].Content, currentChapterByOrder[order].Content))
            .Select(order => sourceChapterByOrder[order].Title)
            .ToList();

        var sourceLessons = sourceChapters
            .SelectMany(c => c.Lessons.Where(l => !l.IsDeleted)
                .Select(l => new
                {
                    Key = $"{c.OrderIndex}|{l.OrderIndex}",
                    Label = $"{c.Title} > {l.Title}",
                    l.Title,
                    l.Content
                }))
            .ToList();

        var currentLessons = currentChapters
            .SelectMany(c => c.Lessons.Where(l => !l.IsDeleted)
                .Select(l => new
                {
                    Key = $"{c.OrderIndex}|{l.OrderIndex}",
                    Label = $"{c.Title} > {l.Title}",
                    l.Title,
                    l.Content
                }))
            .ToList();

        var sourceLessonMap = sourceLessons
            .GroupBy(x => x.Key)
            .ToDictionary(g => g.Key, g => g.First());
        var currentLessonMap = currentLessons
            .GroupBy(x => x.Key)
            .ToDictionary(g => g.Key, g => g.First());

        var addedLessons = sourceLessonMap.Keys
            .Except(currentLessonMap.Keys)
            .Select(key => sourceLessonMap[key].Label)
            .ToList();

        var removedLessons = currentLessonMap.Keys
            .Except(sourceLessonMap.Keys)
            .Select(key => currentLessonMap[key].Label)
            .ToList();

        var updatedLessons = sourceLessonMap.Keys
            .Intersect(currentLessonMap.Keys)
            .Where(key => !TextEquals(sourceLessonMap[key].Title, currentLessonMap[key].Title)
                          || !TextEquals(sourceLessonMap[key].Content, currentLessonMap[key].Content))
            .Select(key => sourceLessonMap[key].Label)
            .ToList();

        var sourceTasks = sourceChapters
            .SelectMany(c => c.Tasks.Select(t => new
            {
                Key = BuildTaskKey(c.OrderIndex, t.TaskType, t.Title),
                Label = $"{c.Title} > {t.Title}",
                t.Description,
                t.Priority,
                t.VerificationPrompt,
                t.MinimumScore,
                t.QuizQuestionsJson
            }))
            .ToList();

        var currentTasks = currentChapters
            .SelectMany(c => c.Tasks.Select(t => new
            {
                Key = BuildTaskKey(c.OrderIndex, t.TaskType, t.Title),
                Label = $"{c.Title} > {t.Title}",
                t.Description,
                t.Priority,
                t.VerificationPrompt,
                t.MinimumScore,
                t.QuizQuestionsJson
            }))
            .ToList();

        var sourceTaskMap = sourceTasks
            .GroupBy(x => x.Key)
            .ToDictionary(g => g.Key, g => g.First());
        var currentTaskMap = currentTasks
            .GroupBy(x => x.Key)
            .ToDictionary(g => g.Key, g => g.First());

        var addedTasks = sourceTaskMap.Keys
            .Except(currentTaskMap.Keys)
            .Select(key => sourceTaskMap[key].Label)
            .ToList();

        var removedTasks = currentTaskMap.Keys
            .Except(sourceTaskMap.Keys)
            .Select(key => currentTaskMap[key].Label)
            .ToList();

        var updatedTasks = sourceTaskMap.Keys
            .Intersect(currentTaskMap.Keys)
            .Where(key => !TextEquals(sourceTaskMap[key].Description, currentTaskMap[key].Description)
                          || sourceTaskMap[key].Priority != currentTaskMap[key].Priority
                          || !TextEquals(sourceTaskMap[key].VerificationPrompt, currentTaskMap[key].VerificationPrompt)
                          || sourceTaskMap[key].MinimumScore != currentTaskMap[key].MinimumScore
                          || !TextEquals(sourceTaskMap[key].QuizQuestionsJson, currentTaskMap[key].QuizQuestionsJson))
            .Select(key => sourceTaskMap[key].Label)
            .ToList();

        return new LearningPathShareUpdateChangeSummaryDto(
            addedChapters.Count,
            removedChapters.Count,
            updatedChapters.Count,
            addedLessons.Count,
            removedLessons.Count,
            updatedLessons.Count,
            addedTasks.Count,
            removedTasks.Count,
            updatedTasks.Count,
            SortDistinct(addedChapters),
            SortDistinct(removedChapters),
            SortDistinct(updatedChapters),
            SortDistinct(addedLessons),
            SortDistinct(removedLessons),
            SortDistinct(updatedLessons),
            SortDistinct(addedTasks),
            SortDistinct(removedTasks),
            SortDistinct(updatedTasks)
        );
    }

    private static bool TextEquals(string? left, string? right)
    {
        var normalizedLeft = string.IsNullOrWhiteSpace(left) ? string.Empty : left.Trim();
        var normalizedRight = string.IsNullOrWhiteSpace(right) ? string.Empty : right.Trim();
        return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildTaskKey(int chapterOrder, TaskType taskType, string taskTitle)
        => $"{chapterOrder}|{(int)taskType}|{Normalize(taskTitle)}";

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Join(' ', value.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static List<string> SortDistinct(List<string> items)
        => items
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
