using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Common.Helpers;
using CodeNexus.Application.Features.LearningPathSkeleton.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Commands.GenerateChapterSkeleton;

public class GenerateChapterSkeletonCommandHandler : IRequestHandler<GenerateChapterSkeletonCommand, Result<ChapterSkeletonDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAIGeneratorService _aiGeneratorService;
    private readonly ITimelineCalculationService _timelineCalculationService;

    public GenerateChapterSkeletonCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IAIGeneratorService aiGeneratorService,
        ITimelineCalculationService timelineCalculationService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _aiGeneratorService = aiGeneratorService;
        _timelineCalculationService = timelineCalculationService;
    }

    public async Task<Result<ChapterSkeletonDto>> Handle(GenerateChapterSkeletonCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.GetUserId();

            var chapter = await _context.Chapters
                .Include(c => c.LearningPath)
                    .ThenInclude(lp => lp.Subject)
                .Include(c => c.LearningPath)
                    .ThenInclude(lp => lp.LearningPathGoals)
                        .ThenInclude(lpg => lpg.Goal)
                .FirstOrDefaultAsync(c => c.PathId == request.PathId && c.OrderIndex == request.OrderIndex, cancellationToken);

            if (chapter == null)
                return Result<ChapterSkeletonDto>.Failure("CHAPTER_NOT_FOUND", "Chapter not found");

            if (chapter.LearningPath.UserId != userId)
                return Result<ChapterSkeletonDto>.Failure("UNAUTHORIZED", "User not authenticated");

            var existingLessons = await _context.Lessons
                .Where(l => l.ChapterId == chapter.ChapterId)
                .OrderBy(l => l.OrderIndex)
                .ToListAsync(cancellationToken);

            if (existingLessons.Any())
            {
                var existingLessonDtos = existingLessons.Select(l => new LessonSkeletonDto(
                    l.LessonId,
                    l.Title,
                    l.OrderIndex,
                    l.LessonDay
                )).ToList();

                return Result<ChapterSkeletonDto>.Success(
                    new ChapterSkeletonDto(
                        chapter.ChapterId,
                        chapter.Title,
                        chapter.OrderIndex,
                        existingLessons.Count,
                        0,
                        existingLessonDtos
                    )
                );
            }

            var complexity = GetComplexityFromLearningPath(chapter.LearningPath);
            var lessonsPerChapter = GetLessonsPerChapter(complexity);
            var language = chapter.LearningPath.Language;

            var chapterData = await GenerateChapterFromAI(
                chapter.LearningPath.Subject.Name,
                BuildGoalSummary(chapter.LearningPath, chapter.LearningPath.Language),
                chapter.LearningPath.Title,
                request.OrderIndex,
                lessonsPerChapter,
                chapter.LearningPath.Language);

            if (chapterData == null || !chapterData.LessonTitles.Any())
            {
                return Result<ChapterSkeletonDto>.Failure("INVALID_AI_RESPONSE", "AI returned invalid response.");
            }

            var lessonSchedules = await _timelineCalculationService.CalculateLessonSchedulesAsync(
                chapter.StartDate!.Value,
                chapter.EndDate!.Value,
                chapterData.LessonTitles.Count,
                complexity,
                cancellationToken);

            var createdLessons = new List<LessonSkeletonDto>();
            for (int i = 0; i < chapterData.LessonTitles.Count && i < lessonSchedules.Count; i++)
            {
                var lessonTitle = chapterData.LessonTitles[i];
                var lessonSchedule = lessonSchedules[i];

                var lesson = new Lesson
                {
                    LessonId = NewId.NextGuid(),
                    ChapterId = chapter.ChapterId,
                    Title = lessonTitle,
                    Content = string.Empty,
                    OrderIndex = i,
                    LessonDay = lessonSchedule.LessonDay,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.Lessons.AddAsync(lesson, cancellationToken);

                createdLessons.Add(new LessonSkeletonDto(
                    lesson.LessonId,
                    lesson.Title,
                    lesson.OrderIndex,
                    lesson.LessonDay
                ));

                var quizzesPerLesson = _timelineCalculationService.GetQuizzesPerLesson(complexity);
                var quizTitles = await GenerateQuizTitlesForLessonAsync(
                    chapter.LearningPath.Subject.Name,
                    chapter.Title,
                    lessonTitle,
                    quizzesPerLesson,
                    language);

                for (int k = 0; k < quizzesPerLesson; k++)
                {
                    var quiz = new Quiz
                    {
                        QuizId = NewId.NextGuid(),
                        LessonId = lesson.LessonId,
                        Title = quizTitles[k],
                        Description = QuizNamingHelper.BuildFallbackDescription(lessonTitle, k, language),
                        DueDate = lessonSchedule.LessonDay.AddDays(2),
                        CreatedAt = DateTime.UtcNow
                    };

                    await _context.Quizzes.AddAsync(quiz, cancellationToken);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            var hasGoalItemMappingChanges = await LearningPathGoalSemanticMappingHelper.RebuildForPathAsync(
                _context,
                _aiGeneratorService,
                chapter.PathId,
                chapter.LearningPath.Language,
                cancellationToken);
            if (hasGoalItemMappingChanges)
            {
                await _context.SaveChangesAsync(cancellationToken);
            }

            return Result<ChapterSkeletonDto>.Success(
                new ChapterSkeletonDto(
                    chapter.ChapterId,
                    chapter.Title,
                    chapter.OrderIndex,
                    chapterData.LessonTitles.Count,
                    0,
                    createdLessons
                )
            );
        }
        catch (Exception ex)
        {
            return Result<ChapterSkeletonDto>.Failure("GENERATION_FAILED", "Failed to generate data.");
        }
    }

    private ComplexityLevel GetComplexityFromLearningPath(LearningPath learningPath)
    {
        if (Enum.TryParse<ComplexityLevel>(learningPath.Status, out var complexity))
            return complexity;

        return ComplexityLevel.Intermediate;
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

    private (int chapters, int lessonsPerChapter, int quizzPercentage, int estimatedDays) CalculateStructureByComplexity(LearningPath learningPath)
    {
        var complexity = Enum.TryParse<ComplexityLevel>(learningPath.Status, out var level) ? level : ComplexityLevel.Beginner;

        return complexity switch
        {
            ComplexityLevel.Beginner => (3, 3, 50, 30),
            ComplexityLevel.Intermediate => (5, 4, 60, 60),
            ComplexityLevel.Advanced => (7, 5, 70, 90),
            _ => (3, 3, 50, 30)
        };
    }

    private async Task<ChapterGenerationData?> GenerateChapterFromAI(
        string subjectName,
        string goalSummary,
        string learningPathTitle,
        int orderIndex,
        int lessonsPerChapter,
        LanguageSelection language)
    {
        var prompt = BuildPrompt(subjectName, goalSummary, learningPathTitle, orderIndex, lessonsPerChapter, language);

        var result = await _aiGeneratorService.GenerateStructureAsync<ChapterGenerationData>(prompt, AIUsageType.StructureGeneration);

        return result;
    }

    private string BuildPrompt(
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
Goal Priorities: {goalSummary}
Learning Path: {learningPathTitle}
Chapter Position: {orderIndex + 1} ({chapterPosition})

{languageInstruction}

REQUIREMENTS:
- Generate {lessonsPerChapter}-5 lesson titles for this chapter
- Chapter should be appropriate for position {orderIndex + 1}
- Respect goal priority percentages when choosing lesson emphasis
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

    private class QuizTitleGenerationData
    {
        public List<string> Titles { get; set; } = new();
    }

    private static string BuildGoalSummary(LearningPath learningPath, LanguageSelection language)
    {
        if (learningPath.LearningPathGoals == null || learningPath.LearningPathGoals.Count == 0)
        {
            return language == LanguageSelection.VietNamese
                ? "Mục tiêu tổng quát (100%)"
                : "General Programming Goal (100%)";
        }

        var ordered = learningPath.LearningPathGoals
            .OrderByDescending(g => g.Weight)
            .Select(g => $"{g.Goal.Title} ({(g.Weight * 100m):0.##}%)")
            .ToList();

        return string.Join(" | ", ordered);
    }
}
