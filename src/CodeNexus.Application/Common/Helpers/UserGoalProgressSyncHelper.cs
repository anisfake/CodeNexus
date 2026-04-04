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
            context.Lessons is null ||
            context.LearnProgresses is null ||
            context.Tasks is null ||
            context.Quizzes is null ||
            context.QuizAttempts is null ||
            context.UserGoalProgresses is null)
        {
            return false;
        }

        var goalIds = await context.LearningPathGoals
            .Where(x => x.PathId == learningPathId)
            .Select(x => x.GoalId)
            .ToListAsync(cancellationToken);

        if (goalIds.Count == 0)
        {
            return false;
        }

        var totalLessons = await context.Lessons
            .Where(l => !l.IsDeleted && !l.Chapter.IsDeleted && l.Chapter.PathId == learningPathId)
            .CountAsync(cancellationToken);

        var completedLessons = await context.LearnProgresses
            .Where(p =>
                p.UserId == userId &&
                p.IsLessonContentRead &&
                !p.Lesson.IsDeleted &&
                !p.Lesson.Chapter.IsDeleted &&
                p.Lesson.Chapter.PathId == learningPathId)
            .Select(p => p.LessonId)
            .Distinct()
            .CountAsync(cancellationToken);

        var totalTasks = await context.Tasks
            .Where(t => t.PathId == learningPathId)
            .CountAsync(cancellationToken);

        var completedTasks = await context.Tasks
            .Where(t => t.PathId == learningPathId && t.Status == TaskStatus_.Completed)
            .CountAsync(cancellationToken);

        var totalQuizzes = await context.Quizzes
            .Where(q =>
                !q.IsDeleted &&
                q.LessonId != null &&
                !q.Lesson!.IsDeleted &&
                !q.Lesson.Chapter.IsDeleted &&
                q.Lesson.Chapter.PathId == learningPathId)
            .CountAsync(cancellationToken);

        var completedQuizzes = await context.QuizAttempts
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
            .CountAsync(cancellationToken);

        var allLessonsCompleted = totalLessons > 0 && completedLessons == totalLessons;
        var allTasksCompleted = totalTasks > 0 && completedTasks == totalTasks;
        var allQuizzesCompleted = totalQuizzes > 0 && completedQuizzes == totalQuizzes;
        var isCompleted = allLessonsCompleted && allTasksCompleted && allQuizzesCompleted;

        var hasAnyLessonProgress = completedLessons > 0;
        var hasAnyTaskProgress = completedTasks > 0;
        var hasAnyQuizProgress = completedQuizzes > 0;
        var hasAnyProgress = hasAnyLessonProgress || hasAnyTaskProgress || hasAnyQuizProgress;

        var targetStatus = isCompleted
            ? GoalProgressStatus.Completed
            : hasAnyProgress
                ? GoalProgressStatus.InProgress
                : GoalProgressStatus.NotStarted;

        var now = DateTime.UtcNow;
        var existingRows = await context.UserGoalProgresses
            .Where(x =>
                x.UserId == userId &&
                x.LearningPathId == learningPathId &&
                goalIds.Contains(x.GoalId))
            .ToListAsync(cancellationToken);

        foreach (var goalId in goalIds)
        {
            var row = existingRows.FirstOrDefault(x => x.GoalId == goalId);
            if (row == null)
            {
                row = new UserGoalProgress
                {
                    UserGoalProgressId = NewId.NextGuid(),
                    UserId = userId,
                    GoalId = goalId,
                    LearningPathId = learningPathId,
                    Status = targetStatus,
                    StartedAt = targetStatus == GoalProgressStatus.NotStarted ? null : now,
                    CompletedAt = targetStatus == GoalProgressStatus.Completed ? now : null,
                    LastUpdatedAt = now
                };
                context.UserGoalProgresses.Add(row);
                continue;
            }

            row.Status = targetStatus;
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

        return true;
    }
}
