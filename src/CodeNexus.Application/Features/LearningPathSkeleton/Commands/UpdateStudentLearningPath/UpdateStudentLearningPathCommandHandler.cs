using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Commands.UpdateStudentLearningPath;

public class UpdateStudentLearningPathCommandHandler : IRequestHandler<UpdateStudentLearningPathCommand, Result<CreateLearningPathResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public UpdateStudentLearningPathCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<CreateLearningPathResponse>> Handle(UpdateStudentLearningPathCommand request, CancellationToken cancellationToken)
    {
        Guid studentId;
        try
        {
            studentId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<CreateLearningPathResponse>.Failure("UNAUTHORIZED", "User not authenticated.");
        }

        var learningPath = await _context.LearningPaths
            .Include(lp => lp.Subject)
            .Include(lp => lp.LearningPathGoals)
                .ThenInclude(lpg => lpg.Goal)
            .Include(lp => lp.User)
            .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                .ThenInclude(c => c.Lessons.Where(l => !l.IsDeleted))
                .ThenInclude(l => l.Quizzes.Where(q => !q.IsDeleted))
                .ThenInclude(q => q.Questions.Where(qq => !qq.IsDeleted))
            .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                .ThenInclude(c => c.Tasks.Where(t => !t.IsDeleted))
            .FirstOrDefaultAsync(lp => lp.PathId == request.PathId && lp.UserId == studentId, cancellationToken);

        if (learningPath == null)
        {
            return Result<CreateLearningPathResponse>.Failure("LEARNING_PATH_NOT_FOUND", "Learning path not found.");
        }

        if (!string.Equals(learningPath.Status, LearningPathStatus.Active.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return Result<CreateLearningPathResponse>.Failure("INVALID_STATUS", "Only active learning paths can be edited.");
        }

        var normalizedChapters = NormalizeStudentChapters(request.Chapters);

        var now = DateTime.UtcNow;
        await SyncChaptersAsync(learningPath, normalizedChapters, now, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        var chapterDtos = BuildChapterDtosFromCurrent(learningPath);

        var goalDtos = learningPath.LearningPathGoals
            .OrderByDescending(g => g.Weight)
            .Select(g => new LearningPathGoalDto(
                g.GoalId,
                g.Goal.Title,
                g.Weight,
                g.Goal.DurationInDays,
                "NotStarted",
                null,
                0m,
                g.Weight * 100m))
            .ToList();

        return Result<CreateLearningPathResponse>.Success(new CreateLearningPathResponse(
            learningPath.PathId,
            learningPath.Title,
            learningPath.Description ?? string.Empty,
            goalDtos,
            chapterDtos,
            chapterDtos.Count,
            learningPath.CreatedAt,
            false,
            learningPath.StartDate,
            learningPath.EndDate,
            learningPath.ComplexityLevel,
            learningPath.Language,
            learningPath.SubjectId,
            learningPath.Subject?.Name ?? string.Empty,
            learningPath.VersionNumber,
            learningPath.VersionNumber,
            false));
    }

    private static List<StudentChapterRequest> NormalizeStudentChapters(List<StudentChapterRequest> chapters)
    {
        var results = new List<StudentChapterRequest>();
        foreach (var chapter in chapters ?? new List<StudentChapterRequest>())
        {
            var lessons = NormalizeStudentLessons(chapter.Lessons, chapter.StartDate);

            var hasChapterData = !string.IsNullOrWhiteSpace(chapter.Title)
                                 || chapter.StartDate.HasValue
                                 || chapter.EndDate.HasValue
                                 || chapter.EstimatedDays.HasValue;

            if (!hasChapterData && lessons.Count == 0)
                continue;

            var title = string.IsNullOrWhiteSpace(chapter.Title)
                ? $"Chapter {results.Count + 1}"
                : chapter.Title.Trim();

            results.Add(chapter with { Title = title, Lessons = lessons });
        }

        return results;
    }

    private static List<StudentLessonRequest> NormalizeStudentLessons(List<StudentLessonRequest> lessons, DateTime? chapterStartDate)
    {
        var results = new List<StudentLessonRequest>();
        foreach (var lesson in lessons ?? new List<StudentLessonRequest>())
        {
            var normalizedContent = lesson.Content is null ? null : lesson.Content.Trim();
            var hasLessonData = !string.IsNullOrWhiteSpace(lesson.Title)
                                || lesson.LessonDay != default
                                || !string.IsNullOrWhiteSpace(normalizedContent);

            if (!hasLessonData)
                continue;

            var title = string.IsNullOrWhiteSpace(lesson.Title)
                ? $"Lesson {results.Count + 1}"
                : lesson.Title.Trim();

            var lessonDay = lesson.LessonDay == default
                ? chapterStartDate?.Date ?? DateTime.UtcNow.Date
                : lesson.LessonDay;

            results.Add(lesson with { Title = title, LessonDay = lessonDay, Content = normalizedContent });
        }

        return results;
    }

    private async Task SyncChaptersAsync(
        LearningPath learningPath,
        List<StudentChapterRequest> requestedChapters,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var existingByOrder = learningPath.Chapters
            .Where(c => !c.IsDeleted)
            .ToDictionary(c => c.OrderIndex);

        for (int chapterIndex = 0; chapterIndex < requestedChapters.Count; chapterIndex++)
        {
            var chapterRequest = requestedChapters[chapterIndex];
            if (!existingByOrder.TryGetValue(chapterIndex, out var chapter))
            {
                chapter = new Chapter
                {
                    ChapterId = NewId.NextGuid(),
                    PathId = learningPath.PathId,
                    CreatedAt = now,
                    IsCompleted = false,
                    Lessons = new List<Lesson>(),
                    Tasks = new List<Domain.Entities.Tasks>()
                };
                learningPath.Chapters.Add(chapter);
                await _context.Chapters.AddAsync(chapter, cancellationToken);
            }

            chapter.Title = chapterRequest.Title.Trim();
            chapter.OrderIndex = chapterIndex;
            chapter.StartDate = chapterRequest.StartDate;
            chapter.EndDate = chapterRequest.EndDate;
            chapter.EstimatedDays = chapterRequest.EstimatedDays ?? CalculateEstimatedDays(chapterRequest.StartDate, chapterRequest.EndDate);
            chapter.IsDeleted = false;
            chapter.DeletedAt = null;
            chapter.UpdatedAt = now;

            await SyncLessonsAsync(chapter, chapterRequest.Lessons, now, cancellationToken);
            // Tasks are intentionally NOT synced — preserved as-is
        }

        foreach (var chapter in learningPath.Chapters.Where(c => !c.IsDeleted && c.OrderIndex >= requestedChapters.Count))
        {
            SoftDeleteChapter(chapter, now);
        }
    }

    private async Task SyncLessonsAsync(
        Chapter chapter,
        List<StudentLessonRequest> requestedLessons,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var existingByOrder = chapter.Lessons
            .Where(l => !l.IsDeleted)
            .ToDictionary(l => l.OrderIndex);

        for (int lessonIndex = 0; lessonIndex < requestedLessons.Count; lessonIndex++)
        {
            var lessonRequest = requestedLessons[lessonIndex];
            if (!existingByOrder.TryGetValue(lessonIndex, out var lesson))
            {
                lesson = new Lesson
                {
                    LessonId = NewId.NextGuid(),
                    ChapterId = chapter.ChapterId,
                    CreatedAt = now,
                    Content = string.Empty,
                    Quizzes = new List<Quiz>()
                };
                chapter.Lessons.Add(lesson);
                await _context.Lessons.AddAsync(lesson, cancellationToken);
            }

            lesson.Title = lessonRequest.Title.Trim();
            lesson.OrderIndex = lessonIndex;
            lesson.LessonDay = lessonRequest.LessonDay;
            if (lessonRequest.Content is not null)
            {
                lesson.Content = lessonRequest.Content;
            }
            lesson.IsDeleted = false;
            lesson.DeletedAt = null;
            lesson.UpdatedAt = now;
            // Quizzes are intentionally NOT synced — preserved as-is
        }

        foreach (var lesson in chapter.Lessons.Where(l => !l.IsDeleted && l.OrderIndex >= requestedLessons.Count))
        {
            SoftDeleteLesson(lesson, now);
        }
    }

    private static void SoftDeleteChapter(Chapter chapter, DateTime now)
    {
        chapter.IsDeleted = true;
        chapter.DeletedAt = now;
        chapter.UpdatedAt = now;

        foreach (var lesson in chapter.Lessons.Where(l => !l.IsDeleted))
        {
            SoftDeleteLesson(lesson, now);
        }

        foreach (var task in chapter.Tasks.Where(t => !t.IsDeleted))
        {
            task.IsDeleted = true;
            task.DeletedAt = now;
            task.UpdatedAt = now;
        }
    }

    private static void SoftDeleteLesson(Lesson lesson, DateTime now)
    {
        lesson.IsDeleted = true;
        lesson.DeletedAt = now;
        lesson.UpdatedAt = now;

        foreach (var quiz in lesson.Quizzes.Where(q => !q.IsDeleted))
        {
            SoftDeleteQuiz(quiz, now);
        }
    }

    private static void SoftDeleteQuiz(Quiz quiz, DateTime now)
    {
        quiz.IsDeleted = true;
        quiz.DeletedAt = now;

        foreach (var question in quiz.Questions.Where(q => !q.IsDeleted))
        {
            question.IsDeleted = true;
            question.DeletedAt = now;
            question.UpdatedAt = now;
        }
    }

    private static int CalculateEstimatedDays(DateTime? startDate, DateTime? endDate)
    {
        if (!startDate.HasValue || !endDate.HasValue)
            return 7;

        var days = (int)Math.Ceiling((endDate.Value.Date - startDate.Value.Date).TotalDays) + 1;
        return Math.Max(days, 1);
    }

    private static List<ChapterDto> BuildChapterDtosFromCurrent(LearningPath learningPath)
    {
        return learningPath.Chapters
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.OrderIndex)
            .Select(chapter => new ChapterDto(
                chapter.ChapterId,
                chapter.Title,
                chapter.Content,
                chapter.OrderIndex,
                chapter.Lessons
                    .Where(lesson => !lesson.IsDeleted)
                    .OrderBy(lesson => lesson.OrderIndex)
                    .Select(lesson => new LessonDto(
                        lesson.LessonId,
                        lesson.Title,
                        lesson.Content,
                        lesson.LessonDay,
                        lesson.Quizzes
                            .Where(quiz => !quiz.IsDeleted)
                            .Select(quiz => new QuizDto(
                                quiz.QuizId,
                                quiz.Title,
                                quiz.Description,
                                quiz.Questions
                                    .Where(q => !q.IsDeleted)
                                    .OrderBy(q => q.OrderIndex ?? int.MaxValue)
                                    .Select(q => new QuestionDto(
                                        q.QuestionId,
                                        q.QuestionText,
                                        q.Type ?? QuestionType.SingleChoice,
                                        string.IsNullOrWhiteSpace(q.Options) ? new List<string>() : q.Options.Split("||").ToList(),
                                        q.CorrectAnswer ?? string.Empty,
                                        q.Points,
                                        q.OrderIndex ?? 0))
                                    .ToList()))
                            .ToList()))
                    .ToList(),
                chapter.Tasks
                    .Where(task => !task.IsDeleted)
                    .Select(task => new TaskDto(
                        task.TaskId,
                        task.Title,
                        task.Description ?? string.Empty,
                        task.TaskType,
                        task.Priority,
                        task.Status,
                        task.DueDate))
                    .ToList()))
            .ToList();
    }
}
