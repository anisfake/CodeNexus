using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Common.Events;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.RegularExpressions;
using GoalEntity = CodeNexus.Domain.Entities.Goals;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Commands.UpdateMentorLearningPathDraft;

public class UpdateMentorLearningPathDraftCommandHandler : IRequestHandler<UpdateMentorLearningPathDraftCommand, Result<CreateLearningPathResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPublisher _publisher;

    public UpdateMentorLearningPathDraftCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IPublisher publisher)
    {
        _context = context;
        _currentUserService = currentUserService;
        _publisher = publisher;
    }

    public async Task<Result<CreateLearningPathResponse>> Handle(UpdateMentorLearningPathDraftCommand request, CancellationToken cancellationToken)
    {
        Guid mentorId;
        try
        {
            mentorId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<CreateLearningPathResponse>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        var mentor = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == mentorId, cancellationToken);

        if (mentor == null)
        {
            return Result<CreateLearningPathResponse>.Failure("USER_NOT_FOUND", "User not found.");
        }

        if (!string.Equals(mentor.Role?.RoleName, "Mentor", StringComparison.OrdinalIgnoreCase))
        {
            return Result<CreateLearningPathResponse>.Failure("ACCESS_DENIED", "Access denied.");
        }

        var learningPath = await _context.LearningPaths
            .Include(lp => lp.LearningPathGoals)
            .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                .ThenInclude(c => c.Lessons.Where(l => !l.IsDeleted))
                .ThenInclude(l => l.Quizzes.Where(q => !q.IsDeleted))
                .ThenInclude(q => q.Questions.Where(qq => !qq.IsDeleted))
            .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                .ThenInclude(c => c.Tasks.Where(t => !t.IsDeleted))
            .FirstOrDefaultAsync(lp => lp.PathId == request.PathId, cancellationToken);

        if (learningPath == null)
        {
            return Result<CreateLearningPathResponse>.Failure("LEARNING_PATH_NOT_FOUND", "Learning path not found.");
        }

        if (learningPath.UserId != mentorId)
        {
            return Result<CreateLearningPathResponse>.Failure("ACCESS_DENIED", "Access denied.");
        }

        var isRevertingFromPublished = string.Equals(learningPath.Status, LearningPathStatus.Published.ToString(), StringComparison.OrdinalIgnoreCase);

        if (!string.Equals(learningPath.Status, LearningPathStatus.Draft.ToString(), StringComparison.OrdinalIgnoreCase)
            && !isRevertingFromPublished)
        {
            return Result<CreateLearningPathResponse>.Failure("INVALID_STATUS", "Invalid status for this operation.");
        }

        var subject = await _context.Subjects
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SubjectId == request.SubjectId, cancellationToken);

        if (subject == null)
        {
            return Result<CreateLearningPathResponse>.Failure("SUBJECT_NOT_FOUND", "Subject not found.");
        }

        var uniqueGoalIds = request.Goals.Select(g => g.GoalId).Distinct().ToList();
        var goals = await _context.Goals
            .Where(g => uniqueGoalIds.Contains(g.GoalId))
            .ToListAsync(cancellationToken);

        if (goals.Count != uniqueGoalIds.Count)
        {
            return Result<CreateLearningPathResponse>.Failure("GOAL_NOT_FOUND", "Goal not found.");
        }

        var systemGoalIds = goals
            .Where(g => g.IsSystemDefined)
            .Select(g => g.GoalId)
            .ToList();

        if (systemGoalIds.Count > 0)
        {
            var mappedSystemGoalIds = await _context.SubjectGoals
                .Where(sg => sg.SubjectId == request.SubjectId && systemGoalIds.Contains(sg.GoalId))
                .Select(sg => sg.GoalId)
                .ToListAsync(cancellationToken);

            if (mappedSystemGoalIds.Count != systemGoalIds.Count)
            {
                return Result<CreateLearningPathResponse>.Failure(
                    "GOAL_SUBJECT_MISMATCH",
                    "Goal is not relevant to the selected subject.");
            }
        }

        var normalizedGoals = NormalizeGoalWeights(request.Goals);
        var goalsWithWeights = normalizedGoals
            .Join(goals, ng => ng.GoalId, g => g.GoalId, (ng, g) => (Goal: g, Weight: ng.Weight))
            .OrderByDescending(g => g.Weight)
            .ToList();

        var normalizedChapters = NormalizeManualChapters(request.Chapters);

        if (!request.IncreaseVersion && !isRevertingFromPublished && IsNoDraftChange(request, normalizedChapters, learningPath, goalsWithWeights))
        {
            var currentChapterDtos = BuildChapterDtosFromCurrent(learningPath);
            var currentGoalDtos = goalsWithWeights
                .Select(g => new LearningPathGoalDto(
                    g.Goal.GoalId,
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
                currentGoalDtos,
                currentChapterDtos,
                currentChapterDtos.Count,
                learningPath.CreatedAt,
                false,
                learningPath.StartDate,
                learningPath.EndDate,
                learningPath.ComplexityLevel,
                learningPath.Language,
                learningPath.SubjectId,
                subject.Name,
                learningPath.VersionNumber,
                learningPath.VersionNumber,
                false));
        }

        var previousVersion = learningPath.VersionNumber;
        var requestedVersion = CalculateRequestedVersion(previousVersion, request.IncreaseVersion, request.VersionUpdateType);

        learningPath.SubjectId = request.SubjectId;
        learningPath.Title = BuildVersionedTitle(request.Title, requestedVersion);
        learningPath.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        learningPath.StartDate = request.StartDate;
        learningPath.EndDate = request.EndDate;
        learningPath.ComplexityLevel = request.ComplexityLevel;
        learningPath.Language = request.LanguageSelection;
        learningPath.Status = LearningPathStatus.Draft.ToString();
        // When reverting a Published path back to Draft, always treat as a change (skip early-exit)
        var wasPublished = isRevertingFromPublished;

        _context.LearningPathGoals.RemoveRange(learningPath.LearningPathGoals);

        foreach (var goalWithWeight in goalsWithWeights)
        {
            await _context.LearningPathGoals.AddAsync(new LearningPathGoal
            {
                PathId = learningPath.PathId,
                GoalId = goalWithWeight.Goal.GoalId,
                Weight = goalWithWeight.Weight
            }, cancellationToken);
        }

        var now = DateTime.UtcNow;
        await SyncChaptersAsync(learningPath, normalizedChapters, now, cancellationToken);
        var chapterDtos = BuildChapterDtosFromCurrent(learningPath);

        learningPath.VersionNumber = requestedVersion;
        var currentVersion = learningPath.VersionNumber;

        await _context.SaveChangesAsync(cancellationToken);

        // Do not notify students when reverting a Published path to Draft (mid-edit state)
        if (!wasPublished)
        {
            await _publisher.Publish(
                new LearningPathDraftVersionUpdatedEvent(
                    learningPath.PathId,
                    mentor.UserId,
                    mentor.Username,
                    currentVersion,
                    now),
                cancellationToken);
        }

        var goalDtos = goalsWithWeights
            .Select(g => new LearningPathGoalDto(
                g.Goal.GoalId,
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
            subject.Name,
            learningPath.VersionNumber,
            previousVersion,
            true));
    }

    private static int CalculateEstimatedDays(DateTime? startDate, DateTime? endDate)
    {
        if (!startDate.HasValue || !endDate.HasValue)
        {
            return 7;
        }

        var days = (int)Math.Ceiling((endDate.Value.Date - startDate.Value.Date).TotalDays) + 1;
        return Math.Max(days, 1);
    }

    private static List<ManualChapterRequest> NormalizeManualChapters(List<ManualChapterRequest>? chapters)
    {
        var results = new List<ManualChapterRequest>();
        foreach (var chapter in chapters ?? new List<ManualChapterRequest>())
        {
            var lessons = NormalizeManualLessons(chapter.Lessons, chapter.StartDate);
            var tasks = NormalizeManualTasks(chapter.Tasks);

            var hasChapterData = !string.IsNullOrWhiteSpace(chapter.Title)
                                 || chapter.StartDate.HasValue
                                 || chapter.EndDate.HasValue
                                 || chapter.EstimatedDays.HasValue;

            if (!hasChapterData && lessons.Count == 0 && tasks.Count == 0)
            {
                continue;
            }

            var title = string.IsNullOrWhiteSpace(chapter.Title)
                ? $"Chapter {results.Count + 1}"
                : chapter.Title.Trim();

            results.Add(chapter with
            {
                Title = title,
                Lessons = lessons,
                Tasks = tasks.Count > 0 ? tasks : null
            });
        }

        return results;
    }

    private static List<ManualLessonRequest> NormalizeManualLessons(List<ManualLessonRequest> lessons, DateTime? chapterStartDate)
    {
        var results = new List<ManualLessonRequest>();
        foreach (var lesson in lessons ?? new List<ManualLessonRequest>())
        {
            var quizzes = NormalizeManualQuizzes(lesson.Quizzes);
            var normalizedContent = lesson.Content is null ? null : lesson.Content.Trim();
            var hasLessonData = !string.IsNullOrWhiteSpace(lesson.Title)
                                || lesson.LessonDay != default
                                || !string.IsNullOrWhiteSpace(normalizedContent);

            if (!hasLessonData && quizzes.Count == 0)
            {
                continue;
            }

            var title = string.IsNullOrWhiteSpace(lesson.Title)
                ? $"Lesson {results.Count + 1}"
                : lesson.Title.Trim();

            var lessonDay = lesson.LessonDay == default
                ? chapterStartDate?.Date ?? DateTime.UtcNow.Date
                : lesson.LessonDay;

            results.Add(lesson with
            {
                Title = title,
                LessonDay = lessonDay,
                Quizzes = quizzes.Count > 0 ? quizzes : null,
                Content = normalizedContent
            });
        }

        return results;
    }

    private static List<ManualQuizRequest> NormalizeManualQuizzes(List<ManualQuizRequest>? quizzes)
    {
        var results = new List<ManualQuizRequest>();
        foreach (var quiz in quizzes ?? new List<ManualQuizRequest>())
        {
            var questions = NormalizeManualQuestions(quiz.Questions);
            var hasQuizData = !string.IsNullOrWhiteSpace(quiz.Title)
                              || !string.IsNullOrWhiteSpace(quiz.Description)
                              || quiz.DueDate.HasValue;

            if (!hasQuizData && questions.Count == 0)
            {
                continue;
            }

            var title = string.IsNullOrWhiteSpace(quiz.Title)
                ? $"Quiz {results.Count + 1}"
                : quiz.Title.Trim();

            results.Add(quiz with
            {
                Title = title,
                Description = string.IsNullOrWhiteSpace(quiz.Description) ? null : quiz.Description.Trim(),
                Questions = questions.Count > 0 ? questions : null
            });
        }

        return results;
    }

    private static List<ManualQuestionRequest> NormalizeManualQuestions(List<ManualQuestionRequest>? questions)
    {
        var results = new List<ManualQuestionRequest>();
        foreach (var question in questions ?? new List<ManualQuestionRequest>())
        {
            var options = (question.Options ?? new List<string>())
                .Where(o => !string.IsNullOrWhiteSpace(o))
                .Select(o => o.Trim())
                .ToList();

            var hasQuestionData = !string.IsNullOrWhiteSpace(question.QuestionText)
                                  || options.Count > 0
                                  || !string.IsNullOrWhiteSpace(question.CorrectAnswer)
                                  || question.Points != 1m;

            if (!hasQuestionData)
            {
                continue;
            }

            results.Add(question with
            {
                QuestionText = string.IsNullOrWhiteSpace(question.QuestionText)
                    ? $"Question {results.Count + 1}"
                    : question.QuestionText.Trim(),
                Options = options.Count > 0 ? options : null,
                CorrectAnswer = string.IsNullOrWhiteSpace(question.CorrectAnswer) ? null : question.CorrectAnswer.Trim()
            });
        }

        return results;
    }

    private static List<ManualTaskRequest> NormalizeManualTasks(List<ManualTaskRequest>? tasks)
    {
        var results = new List<ManualTaskRequest>();
        foreach (var task in tasks ?? new List<ManualTaskRequest>())
        {
            var hasTaskData = !string.IsNullOrWhiteSpace(task.Title)
                              || !string.IsNullOrWhiteSpace(task.Description)
                              || task.DueDate.HasValue
                              || task.Priority.HasValue;

            if (!hasTaskData)
            {
                continue;
            }

            results.Add(task with
            {
                Title = string.IsNullOrWhiteSpace(task.Title) ? $"Task {results.Count + 1}" : task.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(task.Description) ? null : task.Description.Trim()
            });
        }

        return results;
    }

    private async Task SyncChaptersAsync(
        LearningPath learningPath,
        List<ManualChapterRequest> requestedChapters,
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
                    IsCompleted = false
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
            chapter.UpdatedAt = string.IsNullOrWhiteSpace(chapter.Content)
                ? null
                : chapter.UpdatedAt ?? now;

            await SyncLessonsAsync(chapter, chapterRequest.Lessons, now, cancellationToken);
            await SyncTasksAsync(chapter, learningPath.PathId, chapterRequest.Tasks ?? new List<ManualTaskRequest>(), now, cancellationToken);
        }

        foreach (var chapter in learningPath.Chapters.Where(c => !c.IsDeleted && c.OrderIndex >= requestedChapters.Count))
        {
            SoftDeleteChapter(chapter, now);
        }
    }

    private async Task SyncLessonsAsync(
        Chapter chapter,
        List<ManualLessonRequest> requestedLessons,
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
                    Content = string.Empty
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
            lesson.UpdatedAt = string.IsNullOrWhiteSpace(lesson.Content)
                ? null
                : lesson.UpdatedAt ?? now;

            await SyncQuizzesAsync(lesson, lessonRequest.Quizzes ?? new List<ManualQuizRequest>(), now, cancellationToken);
        }

        foreach (var lesson in chapter.Lessons.Where(l => !l.IsDeleted && l.OrderIndex >= requestedLessons.Count))
        {
            SoftDeleteLesson(lesson, now);
        }
    }

    private async Task SyncQuizzesAsync(
        Lesson lesson,
        List<ManualQuizRequest> requestedQuizzes,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var existing = lesson.Quizzes
            .Where(q => !q.IsDeleted)
            .OrderBy(q => q.CreatedAt)
            .ToList();

        for (int quizIndex = 0; quizIndex < requestedQuizzes.Count; quizIndex++)
        {
            var quizRequest = requestedQuizzes[quizIndex];
            var quiz = quizIndex < existing.Count
                ? existing[quizIndex]
                : new Quiz
                {
                    QuizId = NewId.NextGuid(),
                    LessonId = lesson.LessonId,
                    CreatedAt = now
                };

            if (quizIndex >= existing.Count)
            {
                lesson.Quizzes.Add(quiz);
                await _context.Quizzes.AddAsync(quiz, cancellationToken);
            }

            quiz.Title = quizRequest.Title.Trim();
            quiz.Description = string.IsNullOrWhiteSpace(quizRequest.Description) ? null : quizRequest.Description.Trim();
            quiz.DueDate = quizRequest.DueDate;
            quiz.IsDeleted = false;
            quiz.DeletedAt = null;

            await SyncQuestionsAsync(quiz, quizRequest.Questions ?? new List<ManualQuestionRequest>(), now, cancellationToken);
        }

        foreach (var quiz in existing.Skip(requestedQuizzes.Count))
        {
            SoftDeleteQuiz(quiz, now);
        }
    }

    private async Task SyncQuestionsAsync(
        Quiz quiz,
        List<ManualQuestionRequest> requestedQuestions,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var existingByOrder = quiz.Questions
            .Where(q => !q.IsDeleted)
            .ToDictionary(q => q.OrderIndex ?? 0);

        for (int questionIndex = 0; questionIndex < requestedQuestions.Count; questionIndex++)
        {
            var questionRequest = requestedQuestions[questionIndex];
            if (!existingByOrder.TryGetValue(questionIndex, out var question))
            {
                question = new Questions
                {
                    QuestionId = NewId.NextGuid(),
                    QuizId = quiz.QuizId
                };
                quiz.Questions.Add(question);
                await _context.Questions.AddAsync(question, cancellationToken);
            }

            question.QuestionText = questionRequest.QuestionText.Trim();
            question.Type = questionRequest.Type;
            question.Options = questionRequest.Options != null && questionRequest.Options.Count > 0
                ? string.Join("||", questionRequest.Options)
                : null;
            question.CorrectAnswer = string.IsNullOrWhiteSpace(questionRequest.CorrectAnswer)
                ? null
                : questionRequest.CorrectAnswer.Trim();
            question.Points = questionRequest.Points;
            question.OrderIndex = questionIndex;
            question.IsDeleted = false;
            question.DeletedAt = null;
            question.UpdatedAt = now;
        }

        foreach (var question in quiz.Questions.Where(q => !q.IsDeleted && (q.OrderIndex ?? int.MaxValue) >= requestedQuestions.Count))
        {
            question.IsDeleted = true;
            question.DeletedAt = now;
            question.UpdatedAt = now;
        }
    }

    private async Task SyncTasksAsync(
        Chapter chapter,
        Guid pathId,
        List<ManualTaskRequest> requestedTasks,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var existing = chapter.Tasks
            .Where(t => !t.IsDeleted)
            .OrderBy(t => t.CreatedAt)
            .ToList();

        for (int taskIndex = 0; taskIndex < requestedTasks.Count; taskIndex++)
        {
            var taskRequest = requestedTasks[taskIndex];
            var task = taskIndex < existing.Count
                ? existing[taskIndex]
                : new Domain.Entities.Tasks
                {
                    TaskId = NewId.NextGuid(),
                    ChapterId = chapter.ChapterId,
                    PathId = pathId,
                    CreatedAt = now,
                    Status = TaskStatus_.Pending
                };

            if (taskIndex >= existing.Count)
            {
                chapter.Tasks.Add(task);
                await _context.Tasks.AddAsync(task, cancellationToken);
            }

            task.Title = taskRequest.Title.Trim();
            task.Description = string.IsNullOrWhiteSpace(taskRequest.Description) ? null : taskRequest.Description.Trim();
            task.DueDate = taskRequest.DueDate;
            task.Priority = taskRequest.Priority;
            task.TaskType = taskRequest.TaskType;
            task.IsDeleted = false;
            task.DeletedAt = null;
            task.UpdatedAt = now;
        }

        foreach (var task in existing.Skip(requestedTasks.Count))
        {
            task.IsDeleted = true;
            task.DeletedAt = now;
            task.UpdatedAt = now;
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

    private static bool IsNoDraftChange(
        UpdateMentorLearningPathDraftCommand request,
        List<ManualChapterRequest> normalizedChapters,
        LearningPath learningPath,
        IReadOnlyCollection<(GoalEntity Goal, decimal Weight)> requestedGoalsWithWeights)
    {
        if (request.SubjectId != learningPath.SubjectId)
        {
            return false;
        }

        if (!string.Equals(NormalizeBaseTitle(request.Title), NormalizeBaseTitle(learningPath.Title), StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(NormalizeOptionalText(request.Description), NormalizeOptionalText(learningPath.Description), StringComparison.Ordinal))
        {
            return false;
        }

        if (request.ComplexityLevel != learningPath.ComplexityLevel || request.LanguageSelection != learningPath.Language)
        {
            return false;
        }

        if (!GoalsMatchCurrent(learningPath, requestedGoalsWithWeights))
        {
            return false;
        }

        return ChaptersMatchCurrentBySummary(normalizedChapters, learningPath);
    }

    private static bool GoalsMatchCurrent(
        LearningPath learningPath,
        IReadOnlyCollection<(GoalEntity Goal, decimal Weight)> requestedGoalsWithWeights)
    {
        if (learningPath.LearningPathGoals.Count != requestedGoalsWithWeights.Count)
        {
            return false;
        }

        var currentGoalWeights = learningPath.LearningPathGoals
            .ToDictionary(x => x.GoalId, x => x.Weight);

        foreach (var requestedGoal in requestedGoalsWithWeights)
        {
            if (!currentGoalWeights.TryGetValue(requestedGoal.Goal.GoalId, out var currentWeight))
            {
                return false;
            }

            if (Math.Abs(currentWeight - requestedGoal.Weight) > 0.01m)
            {
                return false;
            }
        }

        return true;
    }

    private static bool ChaptersMatchCurrentBySummary(List<ManualChapterRequest> requestedChapters, LearningPath learningPath)
    {
        var currentChapters = learningPath.Chapters
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.OrderIndex)
            .ToList();

        if (currentChapters.Count != requestedChapters.Count)
        {
            return false;
        }

        for (int chapterIndex = 0; chapterIndex < requestedChapters.Count; chapterIndex++)
        {
            var requestedChapter = requestedChapters[chapterIndex];
            var currentChapter = currentChapters[chapterIndex];

            if (!string.Equals(NormalizeRequiredText(requestedChapter.Title), NormalizeRequiredText(currentChapter.Title), StringComparison.Ordinal))
            {
                return false;
            }

            var expectedEstimatedDays = requestedChapter.EstimatedDays
                ?? CalculateEstimatedDays(requestedChapter.StartDate, requestedChapter.EndDate);

            if (currentChapter.EstimatedDays != expectedEstimatedDays)
            {
                return false;
            }

            var currentLessons = currentChapter.Lessons
                .Where(l => !l.IsDeleted)
                .OrderBy(l => l.OrderIndex)
                .ToList();

            if (currentLessons.Count != requestedChapter.Lessons.Count)
            {
                return false;
            }

            for (int lessonIndex = 0; lessonIndex < requestedChapter.Lessons.Count; lessonIndex++)
            {
                var requestedLesson = requestedChapter.Lessons[lessonIndex];
                var currentLesson = currentLessons[lessonIndex];

                if (!string.Equals(NormalizeRequiredText(requestedLesson.Title), NormalizeRequiredText(currentLesson.Title), StringComparison.Ordinal))
                {
                    return false;
                }

                if (requestedLesson.Content is not null
                    && !string.Equals(
                        NormalizeOptionalText(requestedLesson.Content),
                        NormalizeOptionalText(currentLesson.Content),
                        StringComparison.Ordinal))
                {
                    return false;
                }

                var requestedQuizSignatures = (requestedLesson.Quizzes ?? new List<ManualQuizRequest>())
                    .Select(ToQuizSignature)
                    .OrderBy(x => x)
                    .ToList();

                var currentQuizSignatures = currentLesson.Quizzes
                    .Where(q => !q.IsDeleted)
                    .Select(q => $"{NormalizeRequiredText(q.Title)}|{NormalizeOptionalText(q.Description)}|{NormalizeDateTime(q.DueDate)}")
                    .OrderBy(x => x)
                    .ToList();

                if (!requestedQuizSignatures.SequenceEqual(currentQuizSignatures, StringComparer.Ordinal))
                {
                    return false;
                }

            }

            var requestedTaskSignatures = (requestedChapter.Tasks ?? new List<ManualTaskRequest>())
                .Select(ToTaskSignature)
                .OrderBy(x => x)
                .ToList();

            var currentTaskSignatures = currentChapter.Tasks
                .Where(t => !t.IsDeleted)
                .Select(t => $"{NormalizeRequiredText(t.Title)}|{NormalizeOptionalText(t.Description)}|{t.TaskType}|{t.Priority?.ToString() ?? string.Empty}|{NormalizeDateTime(t.DueDate)}")
                .OrderBy(x => x)
                .ToList();

            if (!requestedTaskSignatures.SequenceEqual(currentTaskSignatures, StringComparer.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static string ToQuizSignature(ManualQuizRequest quiz)
        => $"{NormalizeRequiredText(quiz.Title)}|{NormalizeOptionalText(quiz.Description)}|{NormalizeDateTime(quiz.DueDate)}|{NormalizeQuestionSignature(quiz.Questions)}";

    private static string NormalizeQuestionSignature(List<ManualQuestionRequest>? questions)
    {
        return string.Join("#", (questions ?? new List<ManualQuestionRequest>())
            .Select((q, i) =>
                $"{i}|{NormalizeRequiredText(q.QuestionText)}|{q.Type}|{string.Join("||", (q.Options ?? new List<string>()).Select(NormalizeRequiredText))}|{NormalizeOptionalText(q.CorrectAnswer)}|{q.Points}"));
    }

    private static string ToTaskSignature(ManualTaskRequest task)
        => $"{NormalizeRequiredText(task.Title)}|{NormalizeOptionalText(task.Description)}|{task.TaskType}|{task.Priority?.ToString() ?? string.Empty}|{NormalizeDateTime(task.DueDate)}";

    private static string NormalizeDateTime(DateTime? dateTime)
        => dateTime?.Date.ToString("yyyy-MM-dd") ?? string.Empty;

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

    private static string NormalizeOptionalText(string? text)
        => string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();

    private static string NormalizeRequiredText(string? text)
        => string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();

    private static string NormalizeBaseTitle(string? rawTitle)
    {
        var baseTitle = string.IsNullOrWhiteSpace(rawTitle)
            ? "Learning Path"
            : rawTitle.Trim();

        baseTitle = Regex.Replace(baseTitle, @"\s*-\s*ver\s+\d+(\.\d+)?\s*$", string.Empty, RegexOptions.IgnoreCase).Trim();
        baseTitle = Regex.Replace(baseTitle, @"\s+v\d+(\.\d+)?\s*$", string.Empty, RegexOptions.IgnoreCase).Trim();

        return baseTitle;
    }

    private static List<LearningPathGoalRequest> NormalizeGoalWeights(List<LearningPathGoalRequest> goals)
    {
        var total = goals.Sum(g => g.Weight);
        if (total <= 0)
        {
            return goals;
        }

        var normalized = goals
            .Select(g => new LearningPathGoalRequest(g.GoalId, Math.Round((g.Weight / total) * 100m, 2)))
            .ToList();

        var diff = 100m - normalized.Sum(g => g.Weight);
        if (normalized.Count > 0 && diff != 0)
        {
            var top = normalized[0];
            normalized[0] = top with { Weight = top.Weight + diff };
        }

        return normalized;
    }

    private static decimal CalculateRequestedVersion(decimal currentVersion, bool increaseVersion, DraftVersionUpdateType? versionUpdateType)
    {
        if (!increaseVersion)
        {
            return currentVersion;
        }

        var nextVersion = versionUpdateType switch
        {
            DraftVersionUpdateType.Major => Math.Floor(currentVersion) + 1.0m,
            _ => currentVersion + 0.1m
        };

        return Math.Round(nextVersion, 1, MidpointRounding.AwayFromZero);
    }

    private static string FormatVersionLabel(decimal versionNumber)
    {
        return versionNumber.ToString("0.0", CultureInfo.InvariantCulture);
    }

    private static string BuildVersionedTitle(string rawTitle, decimal versionNumber)
    {
        return $"{NormalizeBaseTitle(rawTitle)} - ver {FormatVersionLabel(versionNumber)}";
    }
}
