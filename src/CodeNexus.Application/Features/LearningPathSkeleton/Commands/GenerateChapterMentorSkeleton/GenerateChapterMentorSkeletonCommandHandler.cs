using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPathSkeleton.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Commands.GenerateChapterMentorSkeleton;

public class GenerateChapterMentorSkeletonCommandHandler : IRequestHandler<GenerateChapterMentorSkeletonCommand, Result<GeneratedChapterMentorSkeletonDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAIGeneratorService _aiGeneratorService;

    public GenerateChapterMentorSkeletonCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IAIGeneratorService aiGeneratorService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _aiGeneratorService = aiGeneratorService;
    }

    public async Task<Result<GeneratedChapterMentorSkeletonDto>> Handle(GenerateChapterMentorSkeletonCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ChapterTitle))
        {
            return Result<GeneratedChapterMentorSkeletonDto>.Failure("INVALID_CHAPTER_TITLE", "Chapter title is required.");
        }

        try
        {
            var userId = _currentUserService.GetUserId();

            var learningPath = await _context.LearningPaths
                .Include(lp => lp.Subject)
                .AsNoTracking()
                .FirstOrDefaultAsync(lp => lp.PathId == request.PathId, cancellationToken);

            if (learningPath == null)
            {
                return Result<GeneratedChapterMentorSkeletonDto>.Failure("LEARNING_PATH_NOT_FOUND", "Learning path not found.");
            }

            if (learningPath.UserId != userId)
            {
                return Result<GeneratedChapterMentorSkeletonDto>.Failure("UNAUTHORIZED", "User not authenticated");
            }

            var chapterTitle = request.ChapterTitle.Trim();
            var chapterDescription = string.IsNullOrWhiteSpace(request.ChapterDescription)
                ? null
                : request.ChapterDescription.Trim();

            var targetLessonCount = GetLessonsPerChapter(learningPath.ComplexityLevel);
            var recommendedChapterCount = GetRecommendedChapterCount(learningPath.ComplexityLevel);

            var prompt = BuildPrompt(
                learningPath.Subject.Name,
                learningPath.Title,
                learningPath.Language,
                learningPath.ComplexityLevel,
                chapterTitle,
                chapterDescription,
                targetLessonCount,
                recommendedChapterCount);

            var generatedData = await _aiGeneratorService.GenerateStructureAsync<ChapterMentorGenerationData>(prompt, AIUsageType.StructureGeneration);

            if (generatedData == null || generatedData.LessonTitles.Count == 0)
            {
                return Result<GeneratedChapterMentorSkeletonDto>.Failure("INVALID_AI_RESPONSE", "AI returned invalid response.");
            }

            var lessons = generatedData.LessonTitles
                .Where(title => !string.IsNullOrWhiteSpace(title))
                .Select(title => title.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(targetLessonCount)
                .Select((title, index) => new GeneratedChapterMentorLessonItemDto(title, index))
                .ToList();

            if (lessons.Count == 0)
            {
                return Result<GeneratedChapterMentorSkeletonDto>.Failure("INVALID_AI_RESPONSE", "AI returned invalid response.");
            }

            return Result<GeneratedChapterMentorSkeletonDto>.Success(new GeneratedChapterMentorSkeletonDto(
                learningPath.PathId,
                learningPath.Title,
                learningPath.Language,
                learningPath.ComplexityLevel,
                recommendedChapterCount,
                chapterTitle,
                chapterDescription,
                lessons
            ));
        }
        catch (Exception)
        {
            return Result<GeneratedChapterMentorSkeletonDto>.Failure("GENERATION_FAILED", "Failed to generate chapter mentor skeleton.");
        }
    }

    private static int GetLessonsPerChapter(ComplexityLevel complexity)
    {
        return complexity switch
        {
            ComplexityLevel.Beginner => 4,
            ComplexityLevel.Intermediate => 5,
            ComplexityLevel.Advanced => 6,
            _ => 4
        };
    }

    private static int GetRecommendedChapterCount(ComplexityLevel complexity)
    {
        return complexity switch
        {
            ComplexityLevel.Beginner => 3,
            ComplexityLevel.Intermediate => 5,
            ComplexityLevel.Advanced => 7,
            _ => 3
        };
    }

    private static string BuildPrompt(
        string subjectName,
        string learningPathTitle,
        LanguageSelection language,
        ComplexityLevel complexity,
        string chapterTitle,
        string? chapterDescription,
        int targetLessonCount,
        int recommendedChapterCount)
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

        var chapterDescriptionLine = string.IsNullOrWhiteSpace(chapterDescription)
            ? "Chapter description: Not provided"
            : $"Chapter description: {chapterDescription}";

        return $@"Generate lesson titles for a chapter in JSON format.

Subject: {subjectName}
Learning Path: {learningPathTitle}
Language: {language}
Complexity: {complexity}
Recommended Chapter Count for this complexity: {recommendedChapterCount}
Chapter title: {chapterTitle}
{chapterDescriptionLine}

{languageInstruction}

REQUIREMENTS:
- Generate exactly {targetLessonCount} lesson titles
- Lesson titles must align with chapter title, learning path context, and complexity level
- Lesson titles should progress logically from foundational to deeper concepts

JSON FORMAT:
{{
  ""lessonTitles"": [
    ""Lesson 1 title"",
    ""Lesson 2 title"",
    ""Lesson 3 title""
  ]
}}

IMPORTANT: Return ONLY valid JSON. No markdown, no extra text.";
    }

    private class ChapterMentorGenerationData
    {
        public List<string> LessonTitles { get; set; } = new();
    }
}
