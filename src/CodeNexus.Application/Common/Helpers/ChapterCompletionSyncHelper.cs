using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Common.Helpers;

public static class ChapterCompletionSyncHelper
{
    public static async Task<bool> SyncAsync(
        IApplicationDbContext context,
        Guid chapterId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (context.Chapters is null ||
            context.Lessons is null ||
            context.LearnProgresses is null ||
            context.Tasks is null ||
            context.Quizzes is null ||
            context.QuizAttempts is null)
        {
            return false;
        }

        var chapter = await context.Chapters
            .FirstOrDefaultAsync(c => c.ChapterId == chapterId && !c.IsDeleted, cancellationToken);

        if (chapter == null)
        {
            return false;
        }

        var totalLessons = await context.Lessons
            .Where(l => l.ChapterId == chapterId && !l.IsDeleted)
            .CountAsync(cancellationToken);

        var completedLessons = await context.LearnProgresses
            .Where(p =>
                p.UserId == userId &&
                p.IsLessonContentRead &&
                !p.Lesson.IsDeleted &&
                p.Lesson.ChapterId == chapterId)
            .Select(p => p.LessonId)
            .Distinct()
            .CountAsync(cancellationToken);

        var totalTasks = await context.Tasks
            .Where(t => t.ChapterId == chapterId)
            .CountAsync(cancellationToken);

        var completedTasks = await context.Tasks
            .Where(t => t.ChapterId == chapterId && t.Status == TaskStatus_.Completed)
            .CountAsync(cancellationToken);

        var totalQuizzes = await context.Quizzes
            .Where(q =>
                !q.IsDeleted &&
                q.LessonId.HasValue &&
                !q.Lesson!.IsDeleted &&
                q.Lesson.ChapterId == chapterId)
            .CountAsync(cancellationToken);

        var completedQuizzes = await context.QuizAttempts
            .Where(a =>
                a.UserId == userId &&
                a.Status == QuizAttemptStatus.Passed &&
                !a.Quiz.IsDeleted &&
                a.Quiz.LessonId.HasValue &&
                !a.Quiz.Lesson!.IsDeleted &&
                a.Quiz.Lesson.ChapterId == chapterId)
            .Select(a => a.QuizId)
            .Distinct()
            .CountAsync(cancellationToken);

        var totalItems = totalLessons + totalTasks + totalQuizzes;
        var isCompleted = totalItems > 0
            && completedLessons == totalLessons
            && completedTasks == totalTasks
            && completedQuizzes == totalQuizzes;

        if (chapter.IsCompleted == isCompleted)
        {
            return false;
        }

        chapter.IsCompleted = isCompleted;
        chapter.UpdatedAt = DateTime.UtcNow;
        return true;
    }
}
