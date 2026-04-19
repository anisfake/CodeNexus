using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Common.Helpers;
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
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Commands.GenerateLearningPathSkeleton;

public class GenerateLearningPathSkeletonCommandHandler : IRequestHandler<GenerateLearningPathSkeletonCommand, Result<CreateLearningPathResponse>>
{
    private const decimal UpfrontEstimateSafetyMultiplier = 1.05m;
    private const decimal ChapterRegenerationRatio = 0.20m;
    private const string InsufficientTokenBalanceErrorCode = "INSUFFICIENT_TOKEN_BALANCE";
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITimelineCalculationService _timelineCalculationService;
    private readonly IAIGeneratorService _aiGeneratorService;
    private readonly IPlanUsageLimitService _planUsageLimitService;

    public GenerateLearningPathSkeletonCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ITimelineCalculationService timelineCalculationService,
        IAIGeneratorService aiGeneratorService,
        IPlanUsageLimitService planUsageLimitService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _timelineCalculationService = timelineCalculationService;
        _aiGeneratorService = aiGeneratorService;
        _planUsageLimitService = planUsageLimitService;
    }

    public async Task<Result<CreateLearningPathResponse>> Handle(GenerateLearningPathSkeletonCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.GetUserId();
            var learningPathLimitCheck = await _planUsageLimitService.CheckLearningPathCreationAllowedAsync(userId, cancellationToken);
            if (!learningPathLimitCheck.IsSuccess)
            {
                return Result<CreateLearningPathResponse>.Failure(
                    learningPathLimitCheck.ErrorCode!,
                    learningPathLimitCheck.ErrorMessage!);
            }

            var subject = await _context.Subjects.FirstOrDefaultAsync(x => x.SubjectId == request.SubjectId, cancellationToken: cancellationToken);
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
                .Join(goals, ng => ng.GoalId, g => g.GoalId, (ng, g) => new GoalWeightInfo(g, ng.Weight))
                .OrderByDescending(g => g.Weight)
                .ToList();

            var durationDays = CalculateWeightedDurationDays(goalsWithWeights);
            var plannedStartDate = DateTime.UtcNow;
            var plannedEndDate = plannedStartDate.AddDays(durationDays);

            var chapterTimelines = await _timelineCalculationService.CalculateChapterTimelinesAsync(
                plannedStartDate,
                plannedEndDate,
                0,
                request.ComplexityLevel,
                cancellationToken);

            var upfrontBudgetValidation = await ValidateUpfrontBudgetAsync(
                userId,
                request.ComplexityLevel,
                chapterTimelines.Count,
                cancellationToken);
            if (upfrontBudgetValidation != null)
            {
                return upfrontBudgetValidation;
            }

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
                Status = request.SaveAsDraft
                    ? LearningPathStatus.Draft.ToString()
                    : LearningPathStatus.Active.ToString(),
                StartDate = plannedStartDate,
                EndDate = plannedEndDate,
                CreatedAt = DateTime.UtcNow,
                CreatedByType = true,
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

            var chapters = new List<ChapterDto>();
            var usedChapterKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var usedChapterCores = new List<string>();
            for (int i = 0; i < chapterTimelines.Count; i++)
            {
                var chapterTimeline = chapterTimelines[i];

                var chapterData = await GenerateChapterFromAI(
                    subject.Name,
                    FormatGoalTitlesWithWeights(goalsWithWeights, request.LanguageSelection),
                    learningPath.Title,
                    i,
                    chapterTimelines.Count,
                    request.ComplexityLevel,
                    request.LanguageSelection,
                    cancellationToken);

                chapterData = EnsureValidChapterData(
                    chapterData,
                    subject.Name,
                    learningPath.Title,
                    i,
                    request.ComplexityLevel,
                    request.LanguageSelection);

                var normalizedChapterTitle = NormalizeChapterTitle(
                    chapterData.Title,
                    i,
                    request.LanguageSelection,
                    subject.Name);

                var distinctTitleResult = EnsureDistinctChapterTitle(
                    normalizedChapterTitle,
                    i,
                    request.LanguageSelection,
                    subject.Name,
                    usedChapterKeys,
                    usedChapterCores);

                normalizedChapterTitle = distinctTitleResult.Title;

                if (distinctTitleResult.WasAdjusted)
                {
                    var regenerated = await GenerateChapterFromAIWithFixedTitle(
                        subject.Name,
                        FormatGoalTitlesWithWeights(goalsWithWeights, request.LanguageSelection),
                        learningPath.Title,
                        distinctTitleResult.CoreTitle,
                        i,
                        chapterTimelines.Count,
                        request.ComplexityLevel,
                        request.LanguageSelection,
                        cancellationToken);

                    if (regenerated != null && regenerated.LessonTitles.Count > 0)
                    {
                        chapterData = new ChapterGenerationData
                        {
                            Title = normalizedChapterTitle,
                            Content = regenerated.Content,
                            LessonTitles = regenerated.LessonTitles
                        };
                    }
                }

                var chapter = new Chapter
                {
                    ChapterId = NewId.NextGuid(),
                    PathId = learningPath.PathId,
                    Title = normalizedChapterTitle,
                    Content = chapterData.Content,
                    OrderIndex = i,
                    IsCompleted = false,
                    StartDate = chapterTimeline.StartDate,
                    EndDate = chapterTimeline.EndDate,
                    EstimatedDays = chapterTimeline.EstimatedDays,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = string.IsNullOrWhiteSpace(chapterData.Content) ? null : DateTime.UtcNow
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
                    var quizTitles = await GenerateQuizTitlesForLessonAsync(
                        subject.Name,
                        chapter.Title,
                        lessonTitle,
                        quizzesPerLesson,
                        request.LanguageSelection);

                    for (int k = 0; k < quizzesPerLesson; k++)
                    {
                        var quiz = new Quiz
                        {
                            QuizId = NewId.NextGuid(),
                            LessonId = lesson.LessonId,
                            Title = quizTitles[k],
                            Description = QuizNamingHelper.BuildFallbackDescription(lessonTitle, k, request.LanguageSelection),
                            DueDate = lessonSchedule.LessonDay.AddDays(2),
                            CreatedAt = DateTime.UtcNow
                        };

                        await _context.Quizzes.AddAsync(quiz, cancellationToken);
                    }
                }

                chapters.Add(new ChapterDto(
                    chapter.ChapterId,
                    chapter.Title,
                    chapter.Content,
                    chapter.OrderIndex,
                    lessonDtos,
                    new List<TaskDto>()
                ));
            }

            await _planUsageLimitService.RecordLearningPathCreationUsageAsync(userId, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            var hasGoalItemMappingChanges = await LearningPathGoalSemanticMappingHelper.RebuildForPathAsync(
                _context,
                _aiGeneratorService,
                learningPath.PathId,
                request.LanguageSelection,
                cancellationToken);
            if (hasGoalItemMappingChanges)
            {
                await _context.SaveChangesAsync(cancellationToken);
            }

            var goalDtos = goalsWithWeights.Select(g => new LearningPathGoalDto(
                g.Goal.GoalId,
                g.Goal.Title,
                g.Weight,
                g.Goal.DurationInDays,
                "NotStarted",
                null,
                0m,
                g.Weight * 100m
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
                    true,
                    learningPath.StartDate,
                    learningPath.EndDate,
                    learningPath.ComplexityLevel,
                    learningPath.Language,
                    learningPath.SubjectId,
                    subject.Name
                )
            );
        }
        catch (Exception ex)
        {
            return Result<CreateLearningPathResponse>.Failure("GENERATION_FAILED", "Failed to generate data.");
        }
    }

    private async Task<ChapterGenerationData?> GenerateChapterFromAI(
        string subjectName,
        string goalPrioritySummary,
        string learningPathTitle,
        int orderIndex,
        int totalChapters,
        ComplexityLevel complexity,
        LanguageSelection language,
        CancellationToken cancellationToken)
    {
        var lessonsPerChapter = GetLessonsPerChapter(complexity);
        var prompt = BuildChapterPrompt(
            subjectName,
            goalPrioritySummary,
            learningPathTitle,
            orderIndex,
            totalChapters,
            lessonsPerChapter,
            language);

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
                Content = BuildFallbackChapterContent(subjectName, learningPathTitle, orderIndex, language),
                LessonTitles = Enumerable.Range(1, lessonsPerChapter)
                    .Select(i => $"Lesson {i}: {subjectName} Topic {i}")
                    .ToList()
            };
        }
    }

    private async Task<ChapterGenerationData?> GenerateChapterFromAIWithFixedTitle(
        string subjectName,
        string goalPrioritySummary,
        string learningPathTitle,
        string fixedTitle,
        int orderIndex,
        int totalChapters,
        ComplexityLevel complexity,
        LanguageSelection language,
        CancellationToken cancellationToken)
    {
        var lessonsPerChapter = GetLessonsPerChapter(complexity);
        var prompt = BuildChapterPromptWithFixedTitle(
            subjectName,
            goalPrioritySummary,
            learningPathTitle,
            fixedTitle,
            orderIndex,
            totalChapters,
            lessonsPerChapter,
            language);

        try
        {
            var result = await _aiGeneratorService.GenerateStructureAsync<ChapterGenerationData>(prompt, AIUsageType.StructureGeneration);
            return result;
        }
        catch
        {
            return new ChapterGenerationData
            {
                Title = fixedTitle,
                Content = BuildFallbackChapterContent(subjectName, learningPathTitle, orderIndex, language),
                LessonTitles = Enumerable.Range(1, lessonsPerChapter)
                    .Select(i => $"{fixedTitle} - Lesson {i}")
                    .ToList()
            };
        }
    }

    private async Task<IReadOnlyList<string>> GenerateQuizTitlesForLessonAsync(
        string subjectName,
        string chapterTitle,
        string lessonTitle,
        int quizCount,
        LanguageSelection language)
    {
        if (quizCount <= 0)
        {
            return Array.Empty<string>();
        }

        List<string>? aiTitles = null;
        try
        {
            var prompt = BuildQuizTitlePrompt(subjectName, chapterTitle, lessonTitle, quizCount, language);
            var generated = await _aiGeneratorService.GenerateStructureAsync<QuizTitleGenerationData>(prompt, AIUsageType.StructureGeneration);
            aiTitles = generated?.Titles;
        }
        catch
        {
            // Fallback handled below.
        }

        return QuizNamingHelper.BuildFinalTitles(aiTitles, lessonTitle, quizCount, language);
    }

    private static string BuildQuizTitlePrompt(
        string subjectName,
        string chapterTitle,
        string lessonTitle,
        int quizCount,
        LanguageSelection language)
    {
        var languageInstruction = language switch
        {
            LanguageSelection.VietNamese => @"
=== LANGUAGE REQUIREMENTS ===
- Generate ALL titles in Vietnamese
- Keep technical terms in English where needed (API, JSON, Docker, etc.)
",
            LanguageSelection.English => @"
=== LANGUAGE REQUIREMENTS ===
- Generate ALL titles in English
",
            _ => string.Empty
        };

        return $@"Generate {quizCount} quiz titles for a lesson in JSON format.

=== CONTEXT ===
Subject: {subjectName}
Chapter: {chapterTitle}
Lesson: {lessonTitle}

{languageInstruction}

=== REQUIREMENTS ===
- Return exactly {quizCount} titles
- Each title MUST be clearly related to the lesson
- Titles MUST NOT be identical to the lesson title
- Titles should be concise, specific, and different from each other

=== JSON FORMAT ===
{{
  ""titles"": [
    ""Quiz title 1"",
    ""Quiz title 2""
  ]
}}

IMPORTANT: Return ONLY valid JSON. No markdown, no extra text.";
    }

    private async Task<Result<CreateLearningPathResponse>?> ValidateUpfrontBudgetAsync(
        Guid userId,
        ComplexityLevel complexity,
        int chapterCount,
        CancellationToken cancellationToken)
    {
        if (chapterCount <= 0)
        {
            return null;
        }

        var userAccess = await _context.Users
            .AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => new
            {
                u.TokenBalance,
                RoleName = u.Role != null ? u.Role.RoleName : null
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (userAccess == null)
        {
            return null;
        }

        if (IsPrivilegedRole(userAccess.RoleName) || IsMentorRole(userAccess.RoleName))
        {
            return null;
        }

        if (userAccess.TokenBalance <= 0m)
        {
            return null;
        }

        var paidConfig = await ResolvePaidStructureConfigAsync(cancellationToken);
        if (paidConfig == null)
        {
            return Result<CreateLearningPathResponse>.Failure(
                "PAID_AI_CONFIG_NOT_FOUND",
                "Paid AI configuration for structure generation is missing.");
        }

        var runtimeConfig = ParseRuntimeConfig(paidConfig.ConfigJson);
        var estimatedRequiredTokens = EstimateTotalRequiredTokens(runtimeConfig, complexity, chapterCount);

        if (estimatedRequiredTokens <= 0m)
        {
            return null;
        }

        if (userAccess.TokenBalance < estimatedRequiredTokens)
        {
            return Result<CreateLearningPathResponse>.Failure(
                InsufficientTokenBalanceErrorCode,
                $"Insufficient token balance to generate full learning path. Required about {estimatedRequiredTokens:0} tokens, current balance {userAccess.TokenBalance:0}.");
        }

        return null;
    }

    private async Task<AIProviderConfig?> ResolvePaidStructureConfigAsync(CancellationToken cancellationToken)
    {
        var byUsage = await _context.AIProviderConfigs
            .AsNoTracking()
            .Where(c => c.IsActive && c.AccessTier == AIAccessTier.Paid && c.UsageType == AIUsageType.StructureGeneration)
            .OrderByDescending(c => c.LastUpdated)
            .ThenBy(c => c.ConfigId)
            .FirstOrDefaultAsync(cancellationToken);

        if (byUsage != null)
        {
            return byUsage;
        }

        return await _context.AIProviderConfigs
            .AsNoTracking()
            .Where(c => c.IsActive && c.AccessTier == AIAccessTier.Paid)
            .OrderBy(c => c.UsageType == AIUsageType.StructureGeneration ? 0 : 1)
            .ThenByDescending(c => c.LastUpdated)
            .ThenBy(c => c.ConfigId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private decimal EstimateTotalRequiredTokens(PaidRuntimeConfig runtimeConfig, ComplexityLevel complexity, int chapterCount)
    {
        if (runtimeConfig.InputCostPer1M <= 0m && runtimeConfig.OutputCostPer1M <= 0m)
        {
            return 0m;
        }

        var lessonsPerChapter = GetLessonsPerChapter(complexity);
        var quizzesPerLesson = _timelineCalculationService.GetQuizzesPerLesson(complexity);
        var totalLessons = chapterCount * lessonsPerChapter;
        if (totalLessons <= 0)
        {
            return 0m;
        }

        var inputTokens = Math.Max(runtimeConfig.MaxTokens, 512);

        var metaCalls = 1;
        var chapterCalls = chapterCount;
        var chapterRegenerationCalls = Math.Max(1, (int)Math.Ceiling(chapterCount * ChapterRegenerationRatio));
        var quizTitleCalls = totalLessons;

        var metaOutputTokens = Math.Min(runtimeConfig.MaxTokens, 1024);
        var chapterOutputTokens = Math.Min(runtimeConfig.MaxTokens, 1400 + (lessonsPerChapter * 260));
        var chapterRegenerationOutputTokens = Math.Min(runtimeConfig.MaxTokens, 1100 + (lessonsPerChapter * 220));
        var quizTitleOutputTokens = Math.Min(runtimeConfig.MaxTokens, 280 + (Math.Max(quizzesPerLesson, 1) * 160));

        var metaEstimate = EstimateExpectedChargeTokenAmount(
            inputTokens,
            metaOutputTokens,
            runtimeConfig.InputCostPer1M,
            runtimeConfig.OutputCostPer1M) * metaCalls;

        var chapterEstimate = EstimateExpectedChargeTokenAmount(
            inputTokens,
            chapterOutputTokens,
            runtimeConfig.InputCostPer1M,
            runtimeConfig.OutputCostPer1M) * chapterCalls;

        var chapterRegenerationEstimate = EstimateExpectedChargeTokenAmount(
            inputTokens,
            chapterRegenerationOutputTokens,
            runtimeConfig.InputCostPer1M,
            runtimeConfig.OutputCostPer1M) * chapterRegenerationCalls;

        var quizTitleEstimate = EstimateExpectedChargeTokenAmount(
            inputTokens,
            quizTitleOutputTokens,
            runtimeConfig.InputCostPer1M,
            runtimeConfig.OutputCostPer1M) * quizTitleCalls;

        var baseEstimate = metaEstimate + chapterEstimate + chapterRegenerationEstimate + quizTitleEstimate;
        return Math.Ceiling(baseEstimate * UpfrontEstimateSafetyMultiplier);
    }

    private static decimal EstimateExpectedChargeTokenAmount(
        int inputTokens,
        int outputTokens,
        decimal inputCostPer1M,
        decimal outputCostPer1M)
    {
        const decimal oneMillion = 1_000_000m;
        var rawCharge = ((decimal)inputTokens / oneMillion) * inputCostPer1M
                        + ((decimal)outputTokens / oneMillion) * outputCostPer1M;

        var chargedTokens = Math.Ceiling(rawCharge);
        if (chargedTokens <= 0m && (inputTokens > 0 || outputTokens > 0))
        {
            chargedTokens = 1m;
        }

        return chargedTokens;
    }

    private static PaidRuntimeConfig ParseRuntimeConfig(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
        {
            return PaidRuntimeConfig.Default;
        }

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            };
            var parsed = JsonSerializer.Deserialize<PaidRuntimeConfig>(configJson, options);
            if (parsed == null)
            {
                return PaidRuntimeConfig.Default;
            }

            if (parsed.MaxTokens <= 0)
            {
                parsed.MaxTokens = PaidRuntimeConfig.Default.MaxTokens;
            }

            return parsed;
        }
        catch
        {
            return PaidRuntimeConfig.Default;
        }
    }

    private static bool IsPrivilegedRole(string? roleName)
        => string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase);

    private static bool IsMentorRole(string? roleName)
        => string.Equals(roleName, "Mentor", StringComparison.OrdinalIgnoreCase);

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
        var weightedGoalSummary = FormatGoalTitlesWithWeights(goals, language);

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
Goal Priorities: {weightedGoalSummary}

{languageInstruction}

REQUIREMENTS:
- Title should be short, natural, and professional
- Respect goal priorities when deciding overall emphasis/focus
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
                var normalizedTitle = NormalizeLearningPathTitle(meta.Title, subjectName, goals, language);
                var normalizedDescription = NormalizeLearningPathDescription(meta.Description, subjectName, goals, language);
                return (normalizedTitle, normalizedDescription);
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
        var compactGoals = BuildCompactGoalTags(goals, language);

        return language switch
        {
            LanguageSelection.VietNamese => (
                $"Lộ trình học {subjectName} tập trung vào {compactGoals}.",
                $"Tập trung phát triển kỹ năng {compactGoals} với {subjectName}."
            ),
            LanguageSelection.English => (
                $"{subjectName}: {compactGoals}",
                $"A {subjectName} learning path focused on {compactGoals}."
            ),
            _ => (
                $"{subjectName}: {compactGoals}",
                $"Focused on {compactGoals}."
            )
        };
    }

    private string BuildChapterPrompt(
        string subjectName,
        string goalPrioritySummary,
        string learningPathTitle,
        int orderIndex,
        int totalChapters,
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
Goal Priorities: {goalPrioritySummary}
Learning Path: {learningPathTitle}
Chapter Position: {orderIndex + 1}/{Math.Max(1, totalChapters)} ({chapterPosition})

{languageInstruction}

REQUIREMENTS:
- Generate {lessonsPerChapter} lesson titles for this chapter
- Chapter title should be short and descriptive
- Do NOT include chapter number prefixes like ""Chapter 1"" or ""Chương 1""
- Chapter titles across the whole learning path MUST be distinct (no repeated titles)
- Chapter should be appropriate for position {orderIndex + 1}/{Math.Max(1, totalChapters)}
- Respect goal priority percentages when choosing chapter focus and lesson emphasis
- If there are 2 goals, primary-goal coverage should be broader across the path, but secondary-goal coverage must still be present
- Lessons should progress logically

JSON FORMAT:
{{
  ""title"": ""Chapter title"",
  ""content"": ""A single short sentence describing what this chapter helps the learner achieve."",
  ""lessonTitles"": [
    ""Lesson 1 title"",
    ""Lesson 2 title"",
    ""Lesson 3 title""
  ]
}}

IMPORTANT: Return ONLY valid JSON. No markdown, no extra text.";
    }

    private string BuildChapterPromptWithFixedTitle(
        string subjectName,
        string goalPrioritySummary,
        string learningPathTitle,
        string fixedTitle,
        int orderIndex,
        int totalChapters,
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

        return $@"Generate lesson titles for a chapter in JSON format.

Subject: {subjectName}
Goal Priorities: {goalPrioritySummary}
Learning Path: {learningPathTitle}
Chapter Position: {orderIndex + 1}/{Math.Max(1, totalChapters)}
Fixed Chapter Title: {fixedTitle}

{languageInstruction}

REQUIREMENTS:
- Chapter title MUST be exactly ""{fixedTitle}"" (do not change it)
- Generate {lessonsPerChapter} lesson titles that fit this fixed title
- Respect goal priority percentages when choosing lesson emphasis for this chapter
- Lessons should progress logically

JSON FORMAT:
{{
  ""title"": ""{fixedTitle}"",
  ""content"": ""A single short sentence describing what this chapter helps the learner achieve."",
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
        public string Content { get; set; } = "";
        public List<string> LessonTitles { get; set; } = new();
    }

    private class QuizTitleGenerationData
    {
        public List<string> Titles { get; set; } = new();
    }

    private sealed class PaidRuntimeConfig
    {
        public string Model { get; set; } = string.Empty;
        public int MaxTokens { get; set; } = 8192;
        public decimal InputCostPer1M { get; set; } = 0m;
        public decimal OutputCostPer1M { get; set; } = 0m;

        public static PaidRuntimeConfig Default => new();
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

    private static string FormatGoalTitlesWithWeights(List<GoalWeightInfo> goals, LanguageSelection language)
    {
        if (goals.Count == 0)
        {
            return language == LanguageSelection.VietNamese
                ? "Mục tiêu tổng quát (100%)"
                : "General learning goal (100%)";
        }

        var ordered = goals
            .OrderByDescending(g => g.Weight)
            .Select(g => $"{g.Goal.Title} ({(g.Weight * 100m):0.##}%)")
            .ToList();

        return string.Join(" | ", ordered);
    }

    private sealed class LearningPathMeta
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
    }

    private static string NormalizeLearningPathTitle(
        string title,
        string subjectName,
        List<GoalWeightInfo> goals,
        LanguageSelection language)
    {
        var trimmed = (title ?? string.Empty).Trim();
        var compactGoals = BuildCompactGoalTags(goals, language);

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return BuildLearningPathMetaFallback(subjectName, goals, language).Title;
        }

        var maxLength = language == LanguageSelection.VietNamese ? 70 : 80;
        var lower = trimmed.ToLowerInvariant();
        var hasAnd = lower.Contains(" và ") || lower.Contains(" and ");
        var hasSubject = lower.Contains(subjectName.ToLowerInvariant());

        if (trimmed.Length > maxLength || (hasAnd && trimmed.Length > 55))
        {
            return language == LanguageSelection.VietNamese
                ? $"Lộ trình học {subjectName} tập trung vào {compactGoals}."
                : $"{subjectName}: {compactGoals}";
        }

        if (!hasSubject)
        {
            return $"{subjectName}: {trimmed}";
        }

        return trimmed;
    }

    private static string NormalizeLearningPathDescription(
        string description,
        string subjectName,
        List<GoalWeightInfo> goals,
        LanguageSelection language)
    {
        var trimmed = (description ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return BuildLearningPathMetaFallback(subjectName, goals, language).Description;
        }

        return trimmed;
    }

    private static string BuildCompactGoalTags(List<GoalWeightInfo> goals, LanguageSelection language)
    {
        if (goals.Count == 0)
        {
            return language == LanguageSelection.VietNamese ? "mục tiêu cá nhân" : "personal goals";
        }

        var ordered = goals
            .OrderByDescending(g => g.Weight)
            .Select(g => CompactGoalTitle(g.Goal.Title, language))
            .ToList();

        if (ordered.Count == 1)
        {
            return ordered[0];
        }

        var separator = language == LanguageSelection.VietNamese ? " & " : " & ";
        return $"{ordered[0]}{separator}{ordered[1]}";
    }

    private static string CompactGoalTitle(string title, LanguageSelection language)
    {
        if (string.IsNullOrWhiteSpace(title))
            return language == LanguageSelection.VietNamese ? "mục tiêu" : "goal";

        var normalized = title.Trim();

        normalized = Regex.Replace(normalized, @"\((.*?)\)", string.Empty).Trim();
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();

        if (language == LanguageSelection.VietNamese)
        {
            if (normalized.Contains("Code Quality", StringComparison.OrdinalIgnoreCase))
                return "Code Quality & Review";
            if (normalized.Contains("ML Pipeline", StringComparison.OrdinalIgnoreCase))
                return "ML Pipeline";
        }

        if (normalized.Length > 38)
        {
            return normalized[..38].Trim();
        }

        return normalized;
    }

    private static readonly Regex ChapterPrefixRegex = new(
        @"^(chapter|chương)\s*\d+[\.\:\-]?\s*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex LeadingNumberRegex = new(
        @"^\d+[\.\:\-]\s*",
        RegexOptions.CultureInvariant);

    private static string NormalizeChapterTitle(
        string title,
        int orderIndex,
        LanguageSelection language,
        string subjectName)
    {
        var core = (title ?? string.Empty).Trim();
        core = ChapterPrefixRegex.Replace(core, string.Empty);
        core = LeadingNumberRegex.Replace(core, string.Empty);

        if (string.IsNullOrWhiteSpace(core))
        {
            core = BuildFallbackChapterCore(subjectName, orderIndex, language);
        }

        var prefix = language switch
        {
            LanguageSelection.VietNamese => "Chương",
            _ => "Chapter"
        };

        return $"{prefix} {orderIndex + 1}: {core}";
    }

    private static (string Title, string CoreTitle, bool WasAdjusted) EnsureDistinctChapterTitle(
        string title,
        int orderIndex,
        LanguageSelection language,
        string subjectName,
        HashSet<string> usedKeys,
        List<string> usedCores)
    {
        var core = ExtractCoreTitle(title);
        var key = BuildTitleKey(core);

        if (usedKeys.Add(key) && !IsTooSimilarToExisting(core, usedCores))
        {
            usedCores.Add(core);
            return (title, core, false);
        }

        var updatedCore = BuildThemeChapterCore(orderIndex, language, subjectName);
        var updatedKey = BuildTitleKey(updatedCore);
        if (!usedKeys.Add(updatedKey))
        {
            updatedCore = $"{updatedCore} {orderIndex + 1}";
            usedKeys.Add(BuildTitleKey(updatedCore));
        }
        usedCores.Add(updatedCore);

        var prefix = language switch
        {
            LanguageSelection.VietNamese => "Chương",
            _ => "Chapter"
        };

        return ($"{prefix} {orderIndex + 1}: {updatedCore}", updatedCore, true);
    }

    private static string ExtractCoreTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        var trimmed = title.Trim();
        var withoutPrefix = ChapterPrefixRegex.Replace(trimmed, string.Empty);
        withoutPrefix = LeadingNumberRegex.Replace(withoutPrefix, string.Empty);

        var colonIndex = withoutPrefix.IndexOf(':');
        if (colonIndex >= 0 && colonIndex < withoutPrefix.Length - 1)
        {
            return withoutPrefix[(colonIndex + 1)..].Trim();
        }

        return withoutPrefix.Trim();
    }

    private static string BuildTitleKey(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        var normalized = Regex.Replace(title.ToLowerInvariant(), @"[\p{P}\p{S}]", " ");
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
        return normalized;
    }

    private static bool IsTooSimilarToExisting(string core, List<string> existingCores)
    {
        if (string.IsNullOrWhiteSpace(core) || existingCores.Count == 0)
            return false;

        var currentTokens = Tokenize(core);
        if (currentTokens.Count == 0)
            return false;

        foreach (var existing in existingCores)
        {
            var existingTokens = Tokenize(existing);
            if (existingTokens.Count == 0)
                continue;

            var intersection = currentTokens.Intersect(existingTokens).Count();
            var union = currentTokens.Union(existingTokens).Count();
            if (union == 0)
                continue;

            var similarity = (double)intersection / union;
            if (similarity >= 0.7)
                return true;
        }

        return false;
    }

    private static HashSet<string> Tokenize(string text)
    {
        var normalized = Regex.Replace(text.ToLowerInvariant(), @"[\p{P}\p{S}]", " ");
        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new HashSet<string>(tokens);
    }

    private static string GetChapterTheme(int orderIndex, LanguageSelection language, string subjectName)
    {
        if (language == LanguageSelection.VietNamese)
        {
            var themes = new[]
            {
                "Tổng quan và mục tiêu",
                "Nguyên tắc thiết kế",
                "Chuẩn hoá chất lượng mã",
                "Kiến trúc và thành phần",
                "Pipeline dữ liệu",
                "Huấn luyện và đánh giá",
                "Triển khai và vận hành",
                "Giám sát và tối ưu",
                "Mở rộng và cải tiến"
            };

            return themes[Math.Min(orderIndex, themes.Length - 1)];
        }

        var enThemes = new[]
        {
            "Overview and goals",
            "Design principles",
            "Code quality standards",
            "Architecture and components",
            "Data pipeline",
            "Training and evaluation",
            "Deployment and operations",
            "Monitoring and optimization",
            "Scaling and improvement"
        };

        return enThemes[Math.Min(orderIndex, enThemes.Length - 1)];
    }

    private static string BuildThemeChapterCore(int orderIndex, LanguageSelection language, string subjectName)
    {
        var theme = GetChapterTheme(orderIndex, language, subjectName);
        return language switch
        {
            LanguageSelection.VietNamese => $"{theme} với {subjectName}",
            _ => $"{theme} with {subjectName}"
        };
    }

    private static string BuildFallbackChapterCore(string subjectName, int orderIndex, LanguageSelection language)
    {
        return language switch
        {
            LanguageSelection.VietNamese => orderIndex == 0
                ? $"Giới thiệu về {subjectName}"
                : $"{subjectName} nâng cao {orderIndex + 1}",
            _ => orderIndex == 0
                ? $"Introduction to {subjectName}"
                : $"{subjectName} Topic {orderIndex + 1}"
        };
    }

    private static string BuildFallbackChapterContent(
        string subjectName,
        string learningPathTitle,
        int orderIndex,
        LanguageSelection language)
    {
        return language switch
        {
            LanguageSelection.VietNamese => orderIndex == 0
                ? $"Chương này giới thiệu nền tảng cốt lõi của {subjectName} trong lộ trình {learningPathTitle}."
                : $"Chương này giúp bạn mở rộng kiến thức {subjectName} để tiến gần hơn tới mục tiêu của lộ trình {learningPathTitle}.",
            _ => orderIndex == 0
                ? $"This chapter introduces the core foundations of {subjectName} in the learning path {learningPathTitle}."
                : $"This chapter helps you deepen your {subjectName} skills and move closer to the goals of {learningPathTitle}."
        };
    }

    private ChapterGenerationData EnsureValidChapterData(
        ChapterGenerationData? source,
        string subjectName,
        string learningPathTitle,
        int orderIndex,
        ComplexityLevel complexity,
        LanguageSelection language)
    {
        var lessonsPerChapter = GetLessonsPerChapter(complexity);
        var fallbackTitle = BuildFallbackChapterCore(subjectName, orderIndex, language);
        var fallbackContent = BuildFallbackChapterContent(subjectName, learningPathTitle, orderIndex, language);

        var title = source?.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            title = fallbackTitle;
        }

        var content = source?.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            content = fallbackContent;
        }

        var lessonTitles = source?.LessonTitles?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? new List<string>();

        if (lessonTitles.Count < lessonsPerChapter)
        {
            for (var idx = lessonTitles.Count; idx < lessonsPerChapter; idx++)
            {
                lessonTitles.Add(language == LanguageSelection.VietNamese
                    ? $"Bài {idx + 1}: {subjectName} chuyên đề {idx + 1}"
                    : $"Lesson {idx + 1}: {subjectName} Topic {idx + 1}");
            }
        }
        else if (lessonTitles.Count > lessonsPerChapter)
        {
            lessonTitles = lessonTitles.Take(lessonsPerChapter).ToList();
        }

        return new ChapterGenerationData
        {
            Title = title,
            Content = content,
            LessonTitles = lessonTitles
        };
    }
}

