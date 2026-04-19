using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Helpers;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Commands.AdoptSuggestedLearningPath;

public class AdoptSuggestedLearningPathCommandHandler : IRequestHandler<AdoptSuggestedLearningPathCommand, Result<CreateLearningPathResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITimelineCalculationService _timelineCalculationService;
    private readonly ISubscriptionAccessService _subscriptionAccessService;
    private readonly IPlanUsageLimitService _planUsageLimitService;
    private readonly IAIGeneratorService _aiGeneratorService;

    public AdoptSuggestedLearningPathCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ITimelineCalculationService timelineCalculationService,
        ISubscriptionAccessService subscriptionAccessService,
        IPlanUsageLimitService planUsageLimitService,
        IAIGeneratorService aiGeneratorService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _timelineCalculationService = timelineCalculationService;
        _subscriptionAccessService = subscriptionAccessService;
        _planUsageLimitService = planUsageLimitService;
        _aiGeneratorService = aiGeneratorService;
    }

    public async Task<Result<CreateLearningPathResponse>> Handle(AdoptSuggestedLearningPathCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();
        var learningPathLimitCheck = await _planUsageLimitService.CheckLearningPathCreationAllowedAsync(userId, cancellationToken);
        if (!learningPathLimitCheck.IsSuccess)
        {
            return Result<CreateLearningPathResponse>.Failure(
                learningPathLimitCheck.ErrorCode!,
                learningPathLimitCheck.ErrorMessage!);
        }

        var subject = await _context.Subjects
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SubjectId == request.SubjectId, cancellationToken);

        if (subject == null)
        {
            return Result<CreateLearningPathResponse>.Failure("SUBJECT_NOT_FOUND", "Subject not found.");
        }

        if (request.Goals == null || request.Goals.Count == 0)
        {
            return Result<CreateLearningPathResponse>.Failure("GOALS_REQUIRED", "At least one goal is required");
        }

        if (request.Goals.Count > 2)
        {
            return Result<CreateLearningPathResponse>.Failure("GOALS_LIMIT_EXCEEDED", "You can select up to 2 goals only");
        }

        var uniqueGoalIds = request.Goals.Select(g => g.GoalId).Distinct().ToList();
        if (uniqueGoalIds.Count != request.Goals.Count)
        {
            return Result<CreateLearningPathResponse>.Failure("DUPLICATE_GOALS", "Duplicate goals are not allowed");
        }

        if (request.Goals.All(g => g.Weight <= 0))
        {
            return Result<CreateLearningPathResponse>.Failure("INVALID_GOAL_WEIGHT", "At least one goal must have a positive weight");
        }

        var goals = await _context.Goals
            .AsNoTracking()
            .Where(g => uniqueGoalIds.Contains(g.GoalId) && !g.IsDeleted)
            .ToListAsync(cancellationToken);

        if (goals.Count != uniqueGoalIds.Count)
        {
            return Result<CreateLearningPathResponse>.Failure("GOAL_NOT_FOUND", "Goal not found.");
        }

        var invalidUserGoals = goals
            .Where(g => !g.IsSystemDefined && g.CreatedByUserId != userId)
            .ToList();

        if (invalidUserGoals.Count > 0)
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

        var candidatePath = await _context.LearningPaths
            .AsNoTracking()
            .FirstOrDefaultAsync(lp => lp.PathId == request.SuggestedPathId, cancellationToken);

        if (candidatePath == null)
        {
            return Result<CreateLearningPathResponse>.Failure("LEARNING_PATH_NOT_FOUND", "Learning path not found.");
        }

        if (candidatePath.UserId == userId)
        {
            return Result<CreateLearningPathResponse>.Failure(
                "CANNOT_ADOPT_OWN_PATH",
                "You cannot adopt your own learning path.");
        }

        if (candidatePath.SubjectId != request.SubjectId
            || candidatePath.Language != request.LanguageSelection
            || candidatePath.ComplexityLevel != request.ComplexityLevel)
        {
            return Result<CreateLearningPathResponse>.Failure(
                "SUGGESTION_CONTEXT_MISMATCH",
                "Suggested learning path does not match the selected subject, complexity, or language");
        }

        var normalizedGoals = NormalizeGoalWeights(request.Goals);
        var goalsWithWeights = normalizedGoals
            .Join(goals, ng => ng.GoalId, g => g.GoalId, (ng, g) => new GoalWeightInfo(g, ng.Weight))
            .OrderByDescending(g => g.Weight)
            .ToList();

        var durationDays = CalculateWeightedDurationDays(goalsWithWeights);
        var startDate = DateTime.UtcNow;
        var endDate = startDate.AddDays(durationDays);

        var chapters = await _context.Chapters
            .AsNoTracking()
            .Where(c => c.PathId == candidatePath.PathId && !c.IsDeleted)
            .OrderBy(c => c.OrderIndex)
            .ToListAsync(cancellationToken);

        var candidateChapterIds = chapters.Select(c => c.ChapterId).ToList();

        var lessons = await _context.Lessons
            .AsNoTracking()
            .Where(l => candidateChapterIds.Contains(l.ChapterId) && !l.IsDeleted)
            .OrderBy(l => l.OrderIndex)
            .ToListAsync(cancellationToken);

        var candidateLessonIds = lessons.Select(l => l.LessonId).ToList();

        var quizzes = await _context.Quizzes
            .AsNoTracking()
            .Where(q => q.LessonId.HasValue && candidateLessonIds.Contains(q.LessonId.Value) && !q.IsDeleted)
            .OrderBy(q => q.CreatedAt)
            .ToListAsync(cancellationToken);

        var candidateQuizIds = quizzes.Select(q => q.QuizId).ToList();

        var questions = await _context.Questions
            .AsNoTracking()
            .Where(q => candidateQuizIds.Contains(q.QuizId))
            .OrderBy(q => q.OrderIndex)
            .ToListAsync(cancellationToken);

        var tasks = await _context.Tasks
            .AsNoTracking()
            .Where(t => t.PathId == candidatePath.PathId)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        var newPath = new LearningPath
        {
            PathId = NewId.NextGuid(),
            UserId = userId,
            SubjectId = request.SubjectId,
            Title = candidatePath.Title,
            Description = candidatePath.Description,
            Status = LearningPathStatus.Active.ToString(),
            StartDate = startDate,
            EndDate = endDate,
            CreatedAt = DateTime.UtcNow,
            CreatedByType = candidatePath.CreatedByType,
            Language = request.LanguageSelection,
            ComplexityLevel = request.ComplexityLevel
        };

        await _context.LearningPaths.AddAsync(newPath, cancellationToken);

        foreach (var goalWithWeight in goalsWithWeights)
        {
            await _context.LearningPathGoals.AddAsync(new LearningPathGoal
            {
                PathId = newPath.PathId,
                GoalId = goalWithWeight.Goal.GoalId,
                Weight = goalWithWeight.Weight
            }, cancellationToken);
        }

        var chapterTimelines = await _timelineCalculationService.CalculateChapterTimelinesAsync(
            startDate,
            endDate,
            chapters.Count,
            request.ComplexityLevel,
            cancellationToken);

        if (chapterTimelines.Count != chapters.Count)
        {
            chapterTimelines = BuildEvenChapterTimelines(startDate, endDate, chapters.Count);
        }

        var chapterDtoList = new List<ChapterDto>();

        for (int i = 0; i < chapters.Count; i++)
        {
            var timeline = chapterTimelines[i];
            var sourceChapter = chapters[i];
            var sourceChapterId = sourceChapter.ChapterId;

            var newChapter = new Chapter
            {
                ChapterId = NewId.NextGuid(),
                PathId = newPath.PathId,
                Title = sourceChapter.Title,
                Content = sourceChapter.Content,
                OrderIndex = i,
                IsCompleted = false,
                StartDate = timeline.StartDate,
                EndDate = timeline.EndDate,
                EstimatedDays = timeline.EstimatedDays,
                CreatedAt = DateTime.UtcNow
            };

            await _context.Chapters.AddAsync(newChapter, cancellationToken);

            var sourceLessons = lessons
                .Where(l => l.ChapterId == sourceChapterId)
                .OrderBy(l => l.OrderIndex)
                .ToList();

            var lessonSchedules = await _timelineCalculationService.CalculateLessonSchedulesAsync(
                timeline.StartDate,
                timeline.EndDate,
                sourceLessons.Count,
                request.ComplexityLevel,
                cancellationToken);

            if (lessonSchedules.Count != sourceLessons.Count)
            {
                lessonSchedules = BuildEvenLessonSchedules(timeline.StartDate, timeline.EndDate, sourceLessons.Count);
            }

            var lessonDtos = new List<LessonDto>();
            var taskDtos = new List<TaskDto>();
            var sourceChapterTasks = tasks
                .Where(t => t.ChapterId == sourceChapterId)
                .OrderBy(t => t.CreatedAt)
                .ToList();

            var taskSchedules = await _timelineCalculationService.CalculateTaskSchedulesAsync(
                timeline.StartDate,
                timeline.EndDate,
                sourceChapterTasks.Count,
                request.ComplexityLevel,
                cancellationToken);

            var taskIndex = 0;
            foreach (var sourceTask in sourceChapterTasks)
            {
                var newTask = new CodeNexus.Domain.Entities.Tasks
                {
                    TaskId = NewId.NextGuid(),
                    ChapterId = newChapter.ChapterId,
                    PathId = newPath.PathId,
                    Title = sourceTask.Title,
                    Description = sourceTask.Description,
                    DueDate = taskIndex < taskSchedules.Count ? taskSchedules[taskIndex].DueDate : null,
                    Priority = sourceTask.Priority,
                    Status = TaskStatus_.Pending,
                    CompletedAt = null,
                    TaskType = sourceTask.TaskType,
                    VerificationPrompt = sourceTask.VerificationPrompt,
                    MinimumScore = sourceTask.MinimumScore,
                    QuizQuestionsJson = sourceTask.QuizQuestionsJson,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.Tasks.AddAsync(newTask, cancellationToken);
                taskDtos.Add(new TaskDto(
                    newTask.TaskId,
                    newTask.Title,
                    newTask.Description ?? string.Empty,
                    newTask.TaskType,
                    newTask.Priority,
                    TaskStatus_.Pending,
                    newTask.DueDate,
                    newTask.QuizQuestionsJson));
                taskIndex++;
            }

            for (int j = 0; j < sourceLessons.Count; j++)
            {
                var sourceLesson = sourceLessons[j];
                var schedule = j < lessonSchedules.Count ? lessonSchedules[j].LessonDay : timeline.StartDate;

                var newLesson = new Lesson
                {
                    LessonId = NewId.NextGuid(),
                    ChapterId = newChapter.ChapterId,
                    Title = sourceLesson.Title,
                    Content = sourceLesson.Content,
                    OrderIndex = j,
                    LessonDay = schedule,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.Lessons.AddAsync(newLesson, cancellationToken);

                var sourceQuizzes = quizzes
                    .Where(q => q.LessonId == sourceLesson.LessonId)
                    .OrderBy(q => q.CreatedAt)
                    .ToList();

                var quizDtos = new List<QuizDto>();
                foreach (var sourceQuiz in sourceQuizzes)
                {
                    var newQuiz = new Quiz
                    {
                        QuizId = NewId.NextGuid(),
                        LessonId = newLesson.LessonId,
                        Title = sourceQuiz.Title,
                        Description = sourceQuiz.Description,
                        TimeLimit = sourceQuiz.TimeLimit,
                        PassingScore = sourceQuiz.PassingScore,
                        DueDate = newLesson.LessonDay.AddDays(2),
                        CreatedAt = DateTime.UtcNow,
                        IsDeleted = false
                    };

                    await _context.Quizzes.AddAsync(newQuiz, cancellationToken);

                    var sourceQuestions = questions
                        .Where(q => q.QuizId == sourceQuiz.QuizId)
                        .OrderBy(q => q.OrderIndex)
                        .ToList();

                    foreach (var sourceQuestion in sourceQuestions)
                    {
                        await _context.Questions.AddAsync(new Questions
                        {
                            QuestionId = NewId.NextGuid(),
                            QuizId = newQuiz.QuizId,
                            QuestionText = sourceQuestion.QuestionText,
                            Type = sourceQuestion.Type,
                            Options = sourceQuestion.Options,
                            CorrectAnswer = sourceQuestion.CorrectAnswer,
                            Points = sourceQuestion.Points,
                            OrderIndex = sourceQuestion.OrderIndex
                        }, cancellationToken);
                    }

                    quizDtos.Add(new QuizDto(
                        newQuiz.QuizId,
                        newQuiz.Title,
                        newQuiz.Description ?? string.Empty));
                }

                lessonDtos.Add(new LessonDto(
                    newLesson.LessonId,
                    newLesson.Title,
                    newLesson.Content,
                    newLesson.LessonDay,
                    quizDtos));
            }

            chapterDtoList.Add(new ChapterDto(
                newChapter.ChapterId,
                newChapter.Title,
                newChapter.Content,
                newChapter.OrderIndex,
                lessonDtos,
                taskDtos));
        }

        await _planUsageLimitService.RecordLearningPathCreationUsageAsync(userId, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        var hasGoalItemMappingChanges = await LearningPathGoalSemanticMappingHelper.RebuildForPathAsync(
            _context,
            _aiGeneratorService,
            newPath.PathId,
            request.LanguageSelection,
            cancellationToken);
        if (hasGoalItemMappingChanges)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        var goalDtos = goalsWithWeights
            .Select(g => new LearningPathGoalDto(g.Goal.GoalId, g.Goal.Title, g.Weight, g.Goal.DurationInDays, "NotStarted", null, 0m, g.Weight * 100m))
            .ToList();

        return Result<CreateLearningPathResponse>.Success(new CreateLearningPathResponse(
            newPath.PathId,
            newPath.Title,
            newPath.Description ?? string.Empty,
            goalDtos,
            chapterDtoList,
            chapterDtoList.Count,
            newPath.CreatedAt,
            false,
            null,
            null,
            null,
            null,
            null,
            null,
            newPath.VersionNumber,
            null,
            true));
    }

    private sealed record GoalWeightInfo(CodeNexus.Domain.Entities.Goals Goal, decimal Weight);
    private sealed record NormalizedGoal(Guid GoalId, decimal Weight);

    private static List<NormalizedGoal> NormalizeGoalWeights(List<LearningPathGoalRequest> goals)
    {
        var usePercent = goals.Any(g => g.Weight > 1m);
        var scaled = goals.Select(g => new NormalizedGoal(
            g.GoalId,
            usePercent ? g.Weight / 100m : g.Weight
        )).ToList();

        var sum = scaled.Sum(g => g.Weight);
        if (sum <= 0)
        {
            throw new InvalidOperationException("Goal weights must be greater than 0");
        }

        return scaled.Select(g => new NormalizedGoal(g.GoalId, g.Weight / sum)).ToList();
    }

    private static List<ChapterTimelineDto> BuildEvenChapterTimelines(DateTime startDate, DateTime endDate, int chapterCount)
    {
        var timelines = new List<ChapterTimelineDto>();
        if (chapterCount <= 0)
        {
            return timelines;
        }

        var totalDays = Math.Max(1, (endDate.Date - startDate.Date).Days + 1);
        var baseDays = Math.Max(1, totalDays / chapterCount);
        var remainder = totalDays % chapterCount;
        var cursor = startDate.Date;

        for (int i = 0; i < chapterCount; i++)
        {
            var daysForChapter = baseDays + (i < remainder ? 1 : 0);
            if (i == chapterCount - 1)
            {
                daysForChapter = Math.Max(1, (endDate.Date - cursor).Days + 1);
            }

            var chapterEnd = cursor.AddDays(daysForChapter - 1);
            timelines.Add(new ChapterTimelineDto(i, cursor, chapterEnd, daysForChapter));
            cursor = chapterEnd.AddDays(1);
        }

        return timelines;
    }

    private static List<LessonScheduleDto> BuildEvenLessonSchedules(DateTime startDate, DateTime endDate, int lessonCount)
    {
        var schedules = new List<LessonScheduleDto>();
        if (lessonCount <= 0)
        {
            return schedules;
        }

        var totalDays = Math.Max(1, (endDate.Date - startDate.Date).Days + 1);
        for (int i = 0; i < lessonCount; i++)
        {
            var offset = (int)Math.Floor((double)i * totalDays / lessonCount);
            var lessonDate = startDate.Date.AddDays(Math.Min(offset, totalDays - 1));
            schedules.Add(new LessonScheduleDto(i, lessonDate));
        }

        return schedules;
    }

    private static int CalculateWeightedDurationDays(List<GoalWeightInfo> goals)
    {
        var total = goals.Sum(g => g.Goal.DurationInDays * g.Weight);
        var rounded = (int)Math.Round(total, MidpointRounding.AwayFromZero);
        return Math.Max(1, rounded);
    }
}
