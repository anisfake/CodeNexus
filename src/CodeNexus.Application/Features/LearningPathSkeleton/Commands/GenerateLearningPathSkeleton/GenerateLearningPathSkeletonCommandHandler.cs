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
    private readonly IAIGeneratorService _aiGeneratorService;

    public GenerateLearningPathSkeletonCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IAIGeneratorService aiGeneratorService)
    {
        _context = context;
        _currentUserService = currentUserService;
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

            var goal = await _context.Goals
                .FirstOrDefaultAsync(x => x.GoalId == request.GoalId, cancellationToken: cancellationToken);
            if (goal == null)
            {
                return Result<CreateLearningPathResponse>.Failure("GOAL_NOT_FOUND", "Goal not found");
            }

            var (chapterCount, lessonsPerChapter, quizzPercentage, estimatedDays) = CalculateStructureByComplexity(request.ComplexityLevel);

            LearningPathGenerationData learningPathData;
            try
            {
                learningPathData = await GenerateLearningPathFromAI(
                    subject.Name,
                    goal.Title,
                    goal.Description,
                    chapterCount,
                    lessonsPerChapter,
                    request.ComplexityLevel,
                    request.LanguageSelection);
            }
            catch (Exception ex)
            {
                return Result<CreateLearningPathResponse>.Failure("AI_GENERATION_FAILED", $"Failed to generate learning path: {ex.Message}");
            }

            if (learningPathData == null || string.IsNullOrEmpty(learningPathData.Title))
            {
                return Result<CreateLearningPathResponse>.Failure("INVALID_AI_RESPONSE", "AI returned invalid learning path structure");
            }

            var learningPath = new LearningPath
            {
                PathId = NewId.NextGuid(),
                UserId = userId,
                SubjectId = request.SubjectId,
                GoalId = request.GoalId,
                Title = learningPathData.Title,
                Description = learningPathData.Description,
                Status = "Active",
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(estimatedDays),
                CreatedAt = DateTime.UtcNow,
                CreatedByType = true,
                Language = request.LanguageSelection
            };

            await _context.LearningPaths.AddAsync(learningPath, cancellationToken);

            var chapters = new List<ChapterDto>();
            for (int i = 0; i < learningPathData.Chapters.Count; i++)
            {
                var chapterData = learningPathData.Chapters[i];
                var chapter = new Chapter
                {
                    ChapterId = NewId.NextGuid(),
                    PathId = learningPath.PathId,
                    Title = chapterData.Title,
                    OrderIndex = i,
                    IsCompleted = false,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.Chapters.AddAsync(chapter, cancellationToken);

                var lessonDtos = new List<LessonDto>();
                if (chapterData.Lessons != null)
                {
                    for (int j = 0; j < chapterData.Lessons.Count; j++)
                    {
                        var lesson = new Lesson
                        {
                            LessonId = NewId.NextGuid(),
                            ChapterId = chapter.ChapterId,
                            Title = chapterData.Lessons[j],
                            Content = string.Empty,
                            OrderIndex = j,
                            CreatedAt = DateTime.UtcNow
                        };

                        await _context.Lessons.AddAsync(lesson, cancellationToken);

                        lessonDtos.Add(new LessonDto(
                            lesson.LessonId,
                            lesson.Title,
                            lesson.Content,
                            new List<QuizDto>()
                        ));
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

            return Result<CreateLearningPathResponse>.Success(
                new CreateLearningPathResponse(
                    learningPath.PathId,
                    learningPath.Title,
                    learningPath.Description,
                    chapters,
                    chapterCount,
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

    private (int chapters, int lessonsPerChapter, int quizzPercentage, int estimatedDays) CalculateStructureByComplexity(Domain.Enums.ComplexityLevel complexity)
    {
        return complexity switch
        {
            ComplexityLevel.Beginner => (
                3,     // 3 chapters
                3,     // 3 lessons/chapter = 9 lessons total
                50,    // 50% lessons có quiz
                30     // ~1 tháng
            ),
            ComplexityLevel.Intermediate => (
                5,     // 5 chapters
                4,     // 4 lessons/chapter = 20 lessons total
                60,    // 60% lessons có quiz
                60     // ~2 tháng
            ),
            ComplexityLevel.Advanced => (
                7,     // 7 chapters
                5,     // 5 lessons/chapter = 35 lessons total
                70,    // 70% lessons có quiz
                90     // ~3 tháng
            ),
            _ => (3, 3, 50, 30)        // default
        };
    }

    private async Task<LearningPathGenerationData> GenerateLearningPathFromAI(
        string subjectName,
        string goalTitle,
        string? goalDescription,
        int chapterCount,
        int lessonsPerChapter,
        ComplexityLevel complexity,
        LanguageSelection language)
    {
        var complexityText = complexity switch
        {
            ComplexityLevel.Beginner => "Basic, suitable for beginners.",
            ComplexityLevel.Intermediate => "Average, suitable for those who already have a basic understanding.",
            ComplexityLevel.Advanced => "Advanced level, suitable for those who want to specialize and already have a foundational knowledge.",
            _ => "Basic, suitable for beginners."
        };

        var prompt = BuildPrompt(subjectName, goalTitle, goalDescription, complexityText, chapterCount, lessonsPerChapter, language);
        var learningPathData = await _aiGeneratorService.GenerateStructureAsync<LearningPathGenerationData>(prompt, AIUsageType.StructureGeneration);
        return learningPathData;
    }

    private string BuildPrompt(
        string subjectName,
        string goalTitle,
        string? goalDescription,
        string complexityText,
        int chapterCount,
        int lessonsPerChapter,
        LanguageSelection languageSelection)
    {
        var languageInstruction = languageSelection switch
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

        return $@"Generate a complete learning path structure with chapters and lessons in JSON format.

Subject: {subjectName}
Goal: {goalTitle}
Description: {goalDescription ?? "Not provided"}
Level: {complexityText}

{languageInstruction}

REQUIREMENTS:
- Generate {chapterCount} chapters
- Each chapter should have {lessonsPerChapter}-5 lessons
- Chapters should progress logically from basic to advanced
- Lessons within each chapter should build upon each other
- Focus on practical learning outcomes

JSON FORMAT:
{{
  ""title"": ""Learning path title"",
  ""description"": ""Brief description of what students will learn"",
  ""chapters"": [
    {{
      ""title"": ""Chapter 1 title"",
      ""lessons"": [
        ""Lesson 1.1 title"",
        ""Lesson 1.2 title"",
        ""Lesson 1.3 title""
      ]
    }},
    {{
      ""title"": ""Chapter 2 title"",
      ""lessons"": [
        ""Lesson 2.1 title"",
        ""Lesson 2.2 title"",
        ""Lesson 2.3 title""
      ]
    }}
  ]
}}

IMPORTANT: Return ONLY valid JSON. No markdown, no extra text.";
    }

    private class LearningPathGenerationData
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public List<ChapterGenerationData> Chapters { get; set; } = new();
    }

    private class ChapterGenerationData
    {
        public string Title { get; set; } = "";
        public List<string> Lessons { get; set; } = new();
    }


}