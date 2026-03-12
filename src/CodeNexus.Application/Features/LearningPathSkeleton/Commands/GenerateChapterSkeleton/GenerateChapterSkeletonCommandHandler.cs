using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
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

    public GenerateChapterSkeletonCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IAIGeneratorService aiGeneratorService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _aiGeneratorService = aiGeneratorService;
    }

    public async Task<Result<ChapterSkeletonDto>> Handle(GenerateChapterSkeletonCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.GetUserId();

            var learningPath = await _context.LearningPaths
                .Include(lp => lp.Subject)
                .Include(lp => lp.Goal)
                .FirstOrDefaultAsync(lp => lp.PathId == request.PathId, cancellationToken);

            if (learningPath == null)
                return Result<ChapterSkeletonDto>.Failure("LEARNING_PATH_NOT_FOUND", "Learning path not found");

            if (learningPath.UserId != userId)
                return Result<ChapterSkeletonDto>.Failure("UNAUTHORIZED", "You do not have access to this learning path");

            var (_, lessonsPerChapter, _, _) = CalculateStructureByComplexity(learningPath);

            var chapterData = await GenerateChapterFromAI(
                learningPath.Subject.Name,
                learningPath.Goal.Title,
                learningPath.Title,
                request.OrderIndex,
                lessonsPerChapter,
                learningPath.Language);

            if (chapterData == null || string.IsNullOrEmpty(chapterData.Title))
                return Result<ChapterSkeletonDto>.Failure("INVALID_AI_RESPONSE", "AI returned invalid chapter structure");

            var chapter = new Chapter
            {
                ChapterId = NewId.NextGuid(),
                PathId = learningPath.PathId,
                Title = chapterData.Title,
                OrderIndex = request.OrderIndex,
                IsCompleted = false,
                CreatedAt = DateTime.UtcNow
            };

            await _context.Chapters.AddAsync(chapter, cancellationToken);

            foreach (var lessonTitle in chapterData.LessonTitles)
            {
                var lesson = new Lesson
                {
                    LessonId = NewId.NextGuid(),
                    ChapterId = chapter.ChapterId,
                    Title = lessonTitle,
                    Content = string.Empty,
                    OrderIndex = chapterData.LessonTitles.IndexOf(lessonTitle),
                    CreatedAt = DateTime.UtcNow
                };

                await _context.Lessons.AddAsync(lesson, cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);

            return Result<ChapterSkeletonDto>.Success(
                new ChapterSkeletonDto(
                    chapter.ChapterId,
                    chapter.Title,
                    chapter.OrderIndex,
                    chapterData.LessonTitles.Count,
                    0
                )
            );
        }
        catch (Exception ex)
        {
            return Result<ChapterSkeletonDto>.Failure("GENERATION_FAILED", $"Failed to generate chapter: {ex.Message}");
        }
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
        string goalTitle,
        string learningPathTitle,
        int orderIndex,
        int lessonsPerChapter,
        LanguageSelection language)
    {
        var prompt = BuildPrompt(subjectName, goalTitle, learningPathTitle, orderIndex, lessonsPerChapter, language);

        var result = await _aiGeneratorService.GenerateStructureAsync<ChapterGenerationData>(prompt, AIUsageType.StructureGeneration);

        return result;
    }

    private string BuildPrompt(
        string subjectName,
        string goalTitle,
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
Goal: {goalTitle}
Learning Path: {learningPathTitle}
Chapter Position: {orderIndex + 1} ({chapterPosition})

{languageInstruction}

REQUIREMENTS:
- Generate {lessonsPerChapter}-5 lesson titles for this chapter
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
}
