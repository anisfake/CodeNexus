using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Entities;
using MassTransit;
using MediatR;
using CodeNexus.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Commands.GenerateLearningPathSkeleton;

public class GenerateLearningPathSkeletonCommandHandler : IRequestHandler<GenerateLearningPathSkeletonCommand, Result<CreateLearningPathResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITimelineCalculationService _timelineCalculationService;
    private readonly IAIGeneratorService _aiGeneratorService;

    public GenerateLearningPathSkeletonCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ITimelineCalculationService timelineCalculationService,
        IAIGeneratorService aiGeneratorService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _timelineCalculationService = timelineCalculationService;
        _aiGeneratorService = aiGeneratorService;
    }

    public async Task<Result<CreateLearningPathResponse>> Handle(GenerateLearningPathSkeletonCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.GetUserId();

            var subject = await _context.Subjects.FirstOrDefaultAsync(x => x.SubjectId == request.SubjectId, cancellationToken: cancellationToken);
            if (subject == null)
            {
                return Result<CreateLearningPathResponse>.Failure("SUBJECT_NOT_FOUND", "Subject not found");
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

            var goals = await _context.Goals
                .Where(g => uniqueGoalIds.Contains(g.GoalId))
                .ToListAsync(cancellationToken);

            if (goals.Count != uniqueGoalIds.Count)
            {
                return Result<CreateLearningPathResponse>.Failure("GOAL_NOT_FOUND", "One or more goals were not found");
            }

            var normalizedGoals = NormalizeGoalWeights(request.Goals);
            var goalsWithWeights = normalizedGoals
                .Join(goals, ng => ng.GoalId, g => g.GoalId, (ng, g) => new GoalWeightInfo(g, ng.Weight))
                .OrderByDescending(g => g.Weight)
                .ToList();

            var durationDays = CalculateWeightedDurationDays(goalsWithWeights);
            var (pathTitle, pathDescription) = await GenerateLearningPathMetaAsync(
                subject.Name,
                goalsWithWeights,
                request.LanguageSelection);

            var learningPath = new LearningPath
            {
                PathId = NewId.NextGuid(),
                UserId = userId,
                SubjectId = request.SubjectId,
                Title = pathTitle,
                Description = pathDescription,
                Status = "Active",
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(durationDays),
                CreatedAt = DateTime.UtcNow,
                CreatedByType = true,
                Language = request.LanguageSelection
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

            var chapterTimelines = await _timelineCalculationService.CalculateChapterTimelinesAsync(
                learningPath.StartDate!.Value,
                learningPath.EndDate!.Value,
                0,
                request.ComplexityLevel,
                cancellationToken);

            var chapters = new List<ChapterDto>();
            for (int i = 0; i < chapterTimelines.Count; i++)
            {
                var chapterTimeline = chapterTimelines[i];

                var chapterData = await GenerateChapterFromAI(
                    subject.Name,
                    FormatGoalTitles(goalsWithWeights),
                    learningPath.Title,
                    i,
                    request.ComplexityLevel,
                    request.LanguageSelection,
                    cancellationToken);

                if (chapterData == null || string.IsNullOrEmpty(chapterData.Title))
                {
                    return Result<CreateLearningPathResponse>.Failure("INVALID_AI_RESPONSE", $"AI returned invalid chapter structure for chapter {i + 1}");
                }

                var chapter = new Chapter
                {
                    ChapterId = NewId.NextGuid(),
                    PathId = learningPath.PathId,
                    Title = chapterData.Title,
                    OrderIndex = i,
                    IsCompleted = false,
                    StartDate = chapterTimeline.StartDate,
                    EndDate = chapterTimeline.EndDate,
                    EstimatedDays = chapterTimeline.EstimatedDays,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.Chapters.AddAsync(chapter, cancellationToken);

                var lessonSchedules = await _timelineCalculationService.CalculateLessonSchedulesAsync(
                    chapterTimeline.StartDate,
                    chapterTimeline.EndDate,
                    chapterData.LessonTitles.Count,
                    request.ComplexityLevel,
                    cancellationToken);

                var lessonDtos = new List<LessonDto>();
                for (int j = 0; j < chapterData.LessonTitles.Count && j < lessonSchedules.Count; j++)
                {
                    var lessonTitle = chapterData.LessonTitles[j];
                    var lessonSchedule = lessonSchedules[j];

                    var lesson = new Lesson
                    {
                        LessonId = NewId.NextGuid(),
                        ChapterId = chapter.ChapterId,
                        Title = lessonTitle,
                        Content = string.Empty,
                        OrderIndex = j,
                        LessonDay = lessonSchedule.LessonDay,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _context.Lessons.AddAsync(lesson, cancellationToken);

                    lessonDtos.Add(new LessonDto(
                        lesson.LessonId,
                        lesson.Title,
                        null,
                        lesson.LessonDay,
                        new List<QuizDto>()
                    ));

                    var quizzesPerLesson = _timelineCalculationService.GetQuizzesPerLesson(request.ComplexityLevel);
                    for (int k = 0; k < quizzesPerLesson; k++)
                    {
                        var quiz = new Quiz
                        {
                            QuizId = NewId.NextGuid(),
                            LessonId = lesson.LessonId,
                            Title = $"Quiz {k + 1}: {lessonTitle}",
                            Description = $"Assessment quiz for {lessonTitle}",
                            DueDate = lessonSchedule.LessonDay.AddDays(2),
                            CreatedAt = DateTime.UtcNow
                        };

                        await _context.Quizzes.AddAsync(quiz, cancellationToken);
                    }
                }

                chapters.Add(new ChapterDto(
                    chapter.ChapterId,
                    chapter.Title,
                    null,
                    chapter.OrderIndex,
                    lessonDtos,
                    new List<TaskDto>()
                ));
            }

            await _context.SaveChangesAsync(cancellationToken);

            var goalDtos = goalsWithWeights.Select(g => new LearningPathGoalDto(
                g.Goal.GoalId,
                g.Goal.Title,
                g.Weight,
                g.Goal.DurationInDays
            )).ToList();

            return Result<CreateLearningPathResponse>.Success(
                new CreateLearningPathResponse(
                    learningPath.PathId,
                    learningPath.Title,
                    learningPath.Description,
                    goalDtos,
                    chapters,
                    chapterTimelines.Count,
                    learningPath.CreatedAt,
                    true
                )
            );
        }
        catch (Exception ex)
        {
            return Result<CreateLearningPathResponse>.Failure("GENERATION_FAILED", $"Failed to generate learning path skeleton: {ex.Message}");
        }
    }

    private async Task<ChapterGenerationData?> GenerateChapterFromAI(
        string subjectName,
        string goalSummary,
        string learningPathTitle,
        int orderIndex,
        ComplexityLevel complexity,
        LanguageSelection language,
        CancellationToken cancellationToken)
    {
        var lessonsPerChapter = GetLessonsPerChapter(complexity);
        var prompt = BuildChapterPrompt(subjectName, goalSummary, learningPathTitle, orderIndex, lessonsPerChapter, language);

        try
        {
            var result = await _aiGeneratorService.GenerateStructureAsync<ChapterGenerationData>(prompt, AIUsageType.StructureGeneration);
            return result;
        }
        catch (Exception)
        {
            return new ChapterGenerationData
            {
                Title = $"Chapter {orderIndex + 1}: {subjectName} Fundamentals {orderIndex + 1}",
                LessonTitles = Enumerable.Range(1, lessonsPerChapter)
                    .Select(i => $"Lesson {i}: {subjectName} Topic {i}")
                    .ToList()
            };
        }
    }

    private int GetLessonsPerChapter(ComplexityLevel complexity)
    {
        return complexity switch
        {
            ComplexityLevel.Beginner => 4,
            ComplexityLevel.Intermediate => 5,
            ComplexityLevel.Advanced => 6,
            _ => 4
        };
    }
    private async Task<(string Title, string Description)> GenerateLearningPathMetaAsync(
        string subjectName,
        List<GoalWeightInfo> goals,
        LanguageSelection language)
    {
        var goalTitles = FormatGoalTitles(goals);

        var languageInstruction = language switch
        {
            LanguageSelection.VietNamese => @"
=== LANGUAGE ===
- Use Vietnamese
- Keep technical terms in English
",
            LanguageSelection.English => @"
=== LANGUAGE ===
- Use English
",
            _ => ""
        };

        var prompt = $@"Generate a concise, human-friendly learning path title and description in JSON format.

Subject: {subjectName}
Goals: {goalTitles}

{languageInstruction}

REQUIREMENTS:
- Title should be short, natural, and professional
- Do NOT include percentages or weights
- Do NOT use format ""Learning Path: ..."" or ""Lộ trình học: ..."" literally
- Description should be 1 sentence, clear and friendly

JSON FORMAT:
{{
  ""title"": ""... "",
  ""description"": ""... ""
}}

IMPORTANT: Return ONLY valid JSON. No markdown, no extra text.";

        try
        {
            var meta = await _aiGeneratorService.GenerateStructureAsync<LearningPathMeta>(prompt, AIUsageType.StructureGeneration);
            if (!string.IsNullOrWhiteSpace(meta?.Title) && !string.IsNullOrWhiteSpace(meta.Description))
            {
                return (meta.Title.Trim(), meta.Description.Trim());
            }
        }
        catch
        {

        }

        return BuildLearningPathMetaFallback(subjectName, goals, language);
    }

    private static (string Title, string Description) BuildLearningPathMetaFallback(
        string subjectName,
        List<GoalWeightInfo> goals,
        LanguageSelection language)
    {
        var goalTitles = FormatGoalTitles(goals);

        return language switch
        {
            LanguageSelection.VietNamese => (
                $"Lộ trình học {subjectName}",
                $"Tập trung vào mục tiêu: {goalTitles}."
            ),
            LanguageSelection.English => (
                $"{subjectName} Learning Path",
                $"Focused on goals: {goalTitles}."
            ),
            _ => (
                $"{subjectName} Learning Path",
                $"Focused on goals: {goalTitles}."
            )
        };
    }

    private string BuildChapterPrompt(
        string subjectName,
        string goalSummary,
        string learningPathTitle,
        int orderIndex,
        int lessonsPerChapter,
        LanguageSelection language)
    {
        var languageInstruction = language switch
        {
            LanguageSelection.VietNamese => @"
=== LANGUAGE ===
- Use Vietnamese for descriptions
- Keep technical terms in English (Array, Stack, Queue, API, JSON, etc.)
",
            LanguageSelection.English => @"
=== LANGUAGE ===
- Use English
",
            _ => ""
        };

        var chapterPosition = orderIndex switch
        {
            0 => "first (introduction/basics)",
            _ when orderIndex < 3 => "early (foundational concepts)",
            _ => "advanced (complex topics)"
        };

        return $@"Generate lesson titles for a chapter in JSON format.

Subject: {subjectName}
Goal: {goalSummary}
Learning Path: {learningPathTitle}
Chapter Position: {orderIndex + 1} ({chapterPosition})

{languageInstruction}

REQUIREMENTS:
- Generate {lessonsPerChapter} lesson titles for this chapter
- Chapter should be appropriate for position {orderIndex + 1}
- Lessons should progress logically

JSON FORMAT:
{{
  ""title"": ""Chapter title"",
  ""lessonTitles"": [
    ""Lesson 1 title"",
    ""Lesson 2 title"",
    ""Lesson 3 title""
  ]
}}

IMPORTANT: Return ONLY valid JSON. No markdown, no extra text.";
    }

    private class ChapterGenerationData
    {
        public string Title { get; set; } = "";
        public List<string> LessonTitles { get; set; } = new();
    }


    private sealed record GoalWeightInfo(Domain.Entities.Goals Goal, decimal Weight);

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

    private static int CalculateWeightedDurationDays(List<GoalWeightInfo> goals)
    {
        var total = goals.Sum(g => g.Goal.DurationInDays * g.Weight);
        var rounded = (int)Math.Round(total, MidpointRounding.AwayFromZero);
        return Math.Max(1, rounded);
    }

    private static string FormatGoalTitles(List<GoalWeightInfo> goals)
    {
        if (goals.Count == 1)
        {
            return goals[0].Goal.Title;
        }

        var ordered = goals
            .OrderByDescending(g => g.Weight)
            .Select(g => g.Goal.Title)
            .ToList();

        return $"{ordered[0]} and {ordered[1]}";
    }

    private sealed class LearningPathMeta
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
    }
}







