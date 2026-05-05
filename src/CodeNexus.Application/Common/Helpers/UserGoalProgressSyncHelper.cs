using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Common.Helpers;

public static class UserGoalProgressSyncHelper
{
    public static async Task<bool> SyncForLearningPathAsync(
        IApplicationDbContext context,
        Guid learningPathId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (context.LearningPathGoals is null ||
            context.LearningPathGoalItemMappings is null ||
            context.LearningPaths is null ||
            context.Lessons is null ||
            context.LearnProgresses is null ||
            context.Quizzes is null ||
            context.QuizAttempts is null ||
            context.UserGoalProgresses is null)
        {
            return false;
        }

        var learningPathGoals = await context.LearningPathGoals
            .Where(x => x.PathId == learningPathId)
            .Select(x => new { x.GoalId, x.Weight })
            .ToListAsync(cancellationToken);

        if (learningPathGoals.Count == 0)
        {
            return false;
        }

        var totalLessons = await context.Lessons
            .Where(l => !l.IsDeleted && !l.Chapter.IsDeleted && l.Chapter.PathId == learningPathId)
            .CountAsync(cancellationToken);

        var lessonIds = await context.Lessons
            .Where(l => !l.IsDeleted && !l.Chapter.IsDeleted && l.Chapter.PathId == learningPathId)
            .Select(l => l.LessonId)
            .ToListAsync(cancellationToken);

        var completedLessonIds = await context.LearnProgresses
            .Where(p =>
                p.UserId == userId &&
                p.IsLessonContentRead &&
                !p.Lesson.IsDeleted &&
                !p.Lesson.Chapter.IsDeleted &&
                p.Lesson.Chapter.PathId == learningPathId)
            .Select(p => p.LessonId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var completedLessons = completedLessonIds.Count;

        var totalQuizzes = await context.Quizzes
            .Where(q =>
                !q.IsDeleted &&
                q.LessonId != null &&
                !q.Lesson!.IsDeleted &&
                !q.Lesson.Chapter.IsDeleted &&
                q.Lesson.Chapter.PathId == learningPathId)
            .CountAsync(cancellationToken);

        var quizIds = await context.Quizzes
            .Where(q =>
                !q.IsDeleted &&
                q.LessonId != null &&
                !q.Lesson!.IsDeleted &&
                !q.Lesson.Chapter.IsDeleted &&
                q.Lesson.Chapter.PathId == learningPathId)
            .Select(q => q.QuizId)
            .ToListAsync(cancellationToken);

        var completedQuizIds = await context.QuizAttempts
            .Where(a =>
                a.UserId == userId &&
                a.Status == QuizAttemptStatus.Passed &&
                !a.Quiz.IsDeleted &&
                a.Quiz.LessonId != null &&
                !a.Quiz.Lesson!.IsDeleted &&
                !a.Quiz.Lesson.Chapter.IsDeleted &&
                a.Quiz.Lesson.Chapter.PathId == learningPathId)
            .Select(a => a.QuizId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var completedQuizzes = completedQuizIds.Count;

        var goalIds = learningPathGoals.Select(x => x.GoalId).ToList();
        var mappings = await context.LearningPathGoalItemMappings
            .Where(x => x.PathId == learningPathId && goalIds.Contains(x.GoalId))
            .Select(x => new MappingRow(
                x.GoalId,
                x.ItemId,
                x.ItemType,
                x.RelevanceScore))
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var existingRows = await context.UserGoalProgresses
            .Where(x =>
                x.UserId == userId &&
                x.LearningPathId == learningPathId &&
                goalIds.Contains(x.GoalId))
            .ToListAsync(cancellationToken);

        foreach (var learningPathGoal in learningPathGoals)
        {
            var row = existingRows.FirstOrDefault(x => x.GoalId == learningPathGoal.GoalId);
            var goalTargetPercent = Math.Round(
                Math.Clamp(learningPathGoal.Weight, 0m, 1m) * 100m,
                2);

            var goalMasteryRatio = CalculateGoalMasteryRatio(
                learningPathGoal.GoalId,
                mappings,
                lessonIds,
                completedLessonIds,
                quizIds,
                completedQuizIds);
            var progressPercent = Math.Round(goalMasteryRatio * goalTargetPercent, 2);

            var targetStatus = progressPercent <= 0m
                ? GoalProgressStatus.NotStarted
                : progressPercent >= 100m
                    ? GoalProgressStatus.Completed
                    : GoalProgressStatus.InProgress;

            if (row == null)
            {
                row = new UserGoalProgress
                {
                    UserGoalProgressId = NewId.NextGuid(),
                    UserId = userId,
                    GoalId = learningPathGoal.GoalId,
                    LearningPathId = learningPathId,
                    Status = targetStatus,
                    ProgressPercent = progressPercent,
                    StartedAt = targetStatus == GoalProgressStatus.NotStarted ? null : now,
                    CompletedAt = targetStatus == GoalProgressStatus.Completed ? now : null,
                    LastUpdatedAt = now
                };
                context.UserGoalProgresses.Add(row);
                continue;
            }

            row.Status = targetStatus;
            row.ProgressPercent = progressPercent;
            row.LastUpdatedAt = now;

            if (targetStatus == GoalProgressStatus.NotStarted)
            {
                row.StartedAt = null;
                row.CompletedAt = null;
            }
            else if (targetStatus == GoalProgressStatus.InProgress)
            {
                row.StartedAt ??= now;
                row.CompletedAt = null;
            }
            else
            {
                row.StartedAt ??= now;
                row.CompletedAt ??= now;
            }
        }

        // Sync LearningPath status based on overall completion
        var learningPath = await context.LearningPaths
            .FirstOrDefaultAsync(lp => lp.PathId == learningPathId, cancellationToken);

        if (learningPath != null)
        {
            var totalItems = totalLessons + totalQuizzes;
            var completedItems = completedLessons + completedQuizzes;
            var isPathCompleted = totalItems > 0 && completedItems >= totalItems;

            var targetStatus = isPathCompleted
                ? LearningPathStatus.Completed.ToString()
                : LearningPathStatus.InProgress.ToString();

            if (learningPath.Status != targetStatus &&
                learningPath.Status != LearningPathStatus.Cancelled.ToString() &&
                learningPath.Status != LearningPathStatus.Draft.ToString())
            {
                learningPath.Status = targetStatus;
            }
        }

        return true;
    }

    private static decimal CalculateGoalMasteryRatio(
        Guid goalId,
        IEnumerable<MappingRow> mappings,
        IReadOnlyCollection<Guid> lessonIds,
        IReadOnlyCollection<Guid> completedLessonIds,
        IReadOnlyCollection<Guid> quizIds,
        IReadOnlyCollection<Guid> completedQuizIds)
    {
        var lessonSet = lessonIds.ToHashSet();
        var completedLessonSet = completedLessonIds.ToHashSet();
        var quizSet = quizIds.ToHashSet();
        var completedQuizSet = completedQuizIds.ToHashSet();

        var goalMappings = mappings
            .Where(m => m.GoalId == goalId)
            .ToList();

        if (goalMappings.Count == 0)
        {
            return 0m;
        }

        var typeRatios = new List<decimal>(2);
        typeRatios.AddRange(CalculateTypeRatio(goalMappings, LearningPathGoalItemType.Lesson, lessonSet, completedLessonSet));
        typeRatios.AddRange(CalculateTypeRatio(goalMappings, LearningPathGoalItemType.Quiz, quizSet, completedQuizSet));

        if (typeRatios.Count == 0)
        {
            return 0m;
        }

        var ratio = typeRatios.Average();
        return Math.Clamp(ratio, 0m, 1m);
    }

    private static IEnumerable<decimal> CalculateTypeRatio(
        IEnumerable<MappingRow> goalMappings,
        LearningPathGoalItemType itemType,
        HashSet<Guid> itemIds,
        IReadOnlySet<Guid> completedItemIds)
    {
        if (itemIds.Count == 0)
        {
            yield break;
        }

        var rows = goalMappings
            .Where(m => m.ItemType == itemType && itemIds.Contains(m.ItemId))
            .ToList();

        if (rows.Count == 0)
        {
            yield break;
        }

        decimal totalWeight = rows.Sum(x => x.RelevanceScore);
        if (totalWeight <= 0m)
        {
            totalWeight = rows.Count;
        }

        decimal completedWeight = rows
            .Where(x => completedItemIds.Contains(x.ItemId))
            .Sum(x => x.RelevanceScore);

        if (rows.Count > 0 && completedWeight <= 0m)
        {
            var completedCount = rows.Count(x => completedItemIds.Contains(x.ItemId));
            if (completedCount > 0 && totalWeight > 0m)
            {
                completedWeight = completedCount;
            }
        }

        var ratio = totalWeight <= 0m
            ? 0m
            : Math.Clamp(completedWeight / totalWeight, 0m, 1m);

        yield return ratio;
    }

    private sealed record MappingRow(
        Guid GoalId,
        Guid ItemId,
        LearningPathGoalItemType ItemType,
        decimal RelevanceScore);
}
