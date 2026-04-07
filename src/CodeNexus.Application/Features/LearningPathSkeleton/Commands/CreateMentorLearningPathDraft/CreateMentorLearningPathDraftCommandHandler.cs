using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Commands.CreateMentorLearningPathDraft;

public class CreateMentorLearningPathDraftCommandHandler : IRequestHandler<CreateMentorLearningPathDraftCommand, Result<CreateLearningPathResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public CreateMentorLearningPathDraftCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<CreateLearningPathResponse>> Handle(CreateMentorLearningPathDraftCommand request, CancellationToken cancellationToken)
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
            .Join(goals, ng => ng.GoalId, g => g.GoalId, (ng, g) => new { Goal = g, ng.Weight })
            .OrderByDescending(g => g.Weight)
            .ToList();

        var learningPath = new LearningPath
        {
            PathId = NewId.NextGuid(),
            UserId = mentorId,
            SubjectId = request.SubjectId,
            Title = BuildVersionedTitle(request.Title, 1),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Status = LearningPathStatus.Draft.ToString(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            CreatedAt = DateTime.UtcNow,
            CreatedByType = false,
            Language = request.LanguageSelection,
            ComplexityLevel = request.ComplexityLevel
        };

        await _context.LearningPaths.AddAsync(learningPath, cancellationToken);

        foreach (var goalWithWeight in goalsWithWeights)
        {
            await _context.LearningPathGoals.AddAsync(new LearningPathGoal
            {
                PathId = learningPath.PathId,
                GoalId = goalWithWeight.Goal.GoalId,
                Weight = goalWithWeight.Weight
            }, cancellationToken);
        }

        var chapterDtos = new List<ChapterDto>();
        for (int i = 0; i < request.Chapters.Count; i++)
        {
            var chapterRequest = request.Chapters[i];

            var chapter = new Chapter
            {
                ChapterId = NewId.NextGuid(),
                PathId = learningPath.PathId,
                Title = chapterRequest.Title.Trim(),
                OrderIndex = i,
                IsCompleted = false,
                StartDate = chapterRequest.StartDate,
                EndDate = chapterRequest.EndDate,
                EstimatedDays = chapterRequest.EstimatedDays ?? CalculateEstimatedDays(chapterRequest.StartDate, chapterRequest.EndDate),
                CreatedAt = DateTime.UtcNow
            };

            await _context.Chapters.AddAsync(chapter, cancellationToken);

            var lessonDtos = new List<LessonDto>();
            for (int j = 0; j < chapterRequest.Lessons.Count; j++)
            {
                var lessonRequest = chapterRequest.Lessons[j];

                var lesson = new Lesson
                {
                    LessonId = NewId.NextGuid(),
                    ChapterId = chapter.ChapterId,
                    Title = lessonRequest.Title.Trim(),
                    Content = string.Empty,
                    OrderIndex = j,
                    LessonDay = lessonRequest.LessonDay,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.Lessons.AddAsync(lesson, cancellationToken);

                var quizDtos = new List<QuizDto>();
                foreach (var quizRequest in lessonRequest.Quizzes ?? new List<ManualQuizRequest>())
                {
                    var quiz = new Quiz
                    {
                        QuizId = NewId.NextGuid(),
                        LessonId = lesson.LessonId,
                        Title = quizRequest.Title.Trim(),
                        Description = string.IsNullOrWhiteSpace(quizRequest.Description) ? null : quizRequest.Description.Trim(),
                        DueDate = quizRequest.DueDate,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _context.Quizzes.AddAsync(quiz, cancellationToken);

                    var questionDtos = new List<QuestionDto>();
                    var questionRequests = quizRequest.Questions ?? new List<ManualQuestionRequest>();
                    for (int questionIndex = 0; questionIndex < questionRequests.Count; questionIndex++)
                    {
                        var questionRequest = questionRequests[questionIndex];
                        var question = new Questions
                        {
                            QuestionId = NewId.NextGuid(),
                            QuizId = quiz.QuizId,
                            QuestionText = questionRequest.QuestionText.Trim(),
                            Type = questionRequest.Type,
                            Options = questionRequest.Options != null && questionRequest.Options.Count > 0
                                ? string.Join("||", questionRequest.Options)
                                : null,
                            CorrectAnswer = string.IsNullOrWhiteSpace(questionRequest.CorrectAnswer)
                                ? null
                                : questionRequest.CorrectAnswer.Trim(),
                            Points = questionRequest.Points,
                            OrderIndex = questionIndex
                        };

                        await _context.Questions.AddAsync(question, cancellationToken);

                        questionDtos.Add(new QuestionDto(
                            question.QuestionId,
                            question.QuestionText,
                            question.Type ?? QuestionType.SingleChoice,
                            questionRequest.Options ?? new List<string>(),
                            question.CorrectAnswer ?? string.Empty,
                            question.Points,
                            question.OrderIndex ?? 0));
                    }

                    quizDtos.Add(new QuizDto(
                        quiz.QuizId,
                        quiz.Title,
                        quiz.Description ?? string.Empty,
                        questionDtos));
                }

                lessonDtos.Add(new LessonDto(
                    lesson.LessonId,
                    lesson.Title,
                    lesson.Content,
                    lesson.LessonDay,
                    quizDtos));
            }

            var taskDtos = new List<TaskDto>();
            foreach (var taskRequest in chapterRequest.Tasks ?? new List<ManualTaskRequest>())
            {
                var task = new Domain.Entities.Tasks
                {
                    TaskId = NewId.NextGuid(),
                    ChapterId = chapter.ChapterId,
                    PathId = learningPath.PathId,
                    Title = taskRequest.Title.Trim(),
                    Description = string.IsNullOrWhiteSpace(taskRequest.Description) ? null : taskRequest.Description.Trim(),
                    DueDate = taskRequest.DueDate,
                    Priority = taskRequest.Priority,
                    Status = TaskStatus_.Pending,
                    CreatedAt = DateTime.UtcNow,
                    TaskType = taskRequest.TaskType,
                    QuizQuestionsJson = taskRequest.QuizQuestionsJson
                };

                await _context.Tasks.AddAsync(task, cancellationToken);

                taskDtos.Add(new TaskDto(
                    task.TaskId,
                    task.Title,
                    task.Description ?? string.Empty,
                    task.TaskType,
                    task.Priority,
                    task.Status,
                    task.DueDate,
                    task.QuizQuestionsJson));
            }

            chapterDtos.Add(new ChapterDto(
                chapter.ChapterId,
                chapter.Title,
                chapter.Content,
                chapter.OrderIndex,
                lessonDtos,
                taskDtos));
        }

        await _context.SaveChangesAsync(cancellationToken);

        var goalDtos = goalsWithWeights
            .Select(g => new LearningPathGoalDto(
                g.Goal.GoalId,
                g.Goal.Title,
                g.Weight,
                g.Goal.DurationInDays,
                "NotStarted",
                null))
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
            null,
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

    private static string BuildVersionedTitle(string rawTitle, int versionNumber)
    {
        var baseTitle = string.IsNullOrWhiteSpace(rawTitle)
            ? "Learning Path"
            : rawTitle.Trim();

        baseTitle = Regex.Replace(baseTitle, @"\s*-\s*ver\s+\d+\s*$", string.Empty, RegexOptions.IgnoreCase).Trim();
        baseTitle = Regex.Replace(baseTitle, @"\s+v\d+\s*$", string.Empty, RegexOptions.IgnoreCase).Trim();

        return $"{baseTitle} - ver {versionNumber}";
    }
}
