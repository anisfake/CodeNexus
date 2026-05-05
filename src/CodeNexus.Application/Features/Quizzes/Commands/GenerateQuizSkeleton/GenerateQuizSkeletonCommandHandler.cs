using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Common.Helpers;
using CodeNexus.Application.Features.Quizzes.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Quizzes.Commands.GenerateQuizSkeleton;

public class GenerateQuizSkeletonCommandHandler : IRequestHandler<GenerateQuizSkeletonCommand, Result<GeneratedQuizSkeletonDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAIGeneratorService _aiGeneratorService;

    public GenerateQuizSkeletonCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IAIGeneratorService aiGeneratorService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _aiGeneratorService = aiGeneratorService;
    }

    public async Task<Result<GeneratedQuizSkeletonDto>> Handle(GenerateQuizSkeletonCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.GetUserId();

            var lesson = await _context.Lessons
                .Include(l => l.Chapter)
                    .ThenInclude(c => c.LearningPath)
                        .ThenInclude(lp => lp.Subject)
                .Include(l => l.Quizzes)
                .FirstOrDefaultAsync(l => l.LessonId == request.LessonId, cancellationToken);

            if (lesson == null)
                return Result<GeneratedQuizSkeletonDto>.Failure("LESSON_NOT_FOUND", "Lesson not found");

            if (lesson.Chapter.LearningPath.UserId != userId)
                return Result<GeneratedQuizSkeletonDto>.Failure("UNAUTHORIZED", "User not authenticated");

            if (lesson.Quizzes.Any())
            {
                var existingQuizzes = lesson.Quizzes.Select(q => new QuizSkeletonDto(
                    q.QuizId,
                    q.Title,
                    q.Description,
                    q.DueDate,
                    q.TimeLimit,
                    q.PassingScore
                )).ToList();

                return Result<GeneratedQuizSkeletonDto>.Success(new GeneratedQuizSkeletonDto(existingQuizzes));
            }

            var quizCount = DetermineQuizCount(lesson);
            if (quizCount == 0)
            {
                return Result<GeneratedQuizSkeletonDto>.Success(new GeneratedQuizSkeletonDto(new List<QuizSkeletonDto>()));
            }

            var language = lesson.Chapter.LearningPath.Language;
            var prompt = BuildPrompt(lesson, quizCount, language);
            var generatedData = await _aiGeneratorService.GenerateStructureAsync<QuizGenerationData>(prompt, AIUsageType.StructureGeneration);

            if (generatedData?.Quizzes == null || generatedData.Quizzes.Count == 0)
                return Result<GeneratedQuizSkeletonDto>.Failure("INVALID_AI_RESPONSE", "AI returned invalid response.");

            var lessonTitle = lesson.Title ?? string.Empty;
            var finalTitles = QuizNamingHelper.BuildFinalTitles(
                generatedData.Quizzes.Select(q => q.Title),
                lessonTitle,
                quizCount,
                language);

            var createdQuizzes = new List<QuizSkeletonDto>();
            for (var i = 0; i < quizCount; i++)
            {
                var aiDescription = i < generatedData.Quizzes.Count
                    ? generatedData.Quizzes[i].Description
                    : null;

                var quiz = new Quiz
                {
                    QuizId = NewId.NextGuid(),
                    LessonId = lesson.LessonId,
                    Title = finalTitles[i],
                    Description = string.IsNullOrWhiteSpace(aiDescription)
                        ? QuizNamingHelper.BuildFallbackDescription(lessonTitle, i, language)
                        : aiDescription.Trim(),
                    TimeLimit = null,
                    PassingScore = null,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.Quizzes.AddAsync(quiz, cancellationToken);

                createdQuizzes.Add(new QuizSkeletonDto(
                    quiz.QuizId,
                    quiz.Title,
                    quiz.Description,
                    quiz.DueDate,
                    quiz.TimeLimit,
                    quiz.PassingScore
                ));
            }

            await _context.SaveChangesAsync(cancellationToken);

            var hasGoalItemMappingChanges = await LearningPathGoalSemanticMappingHelper.RebuildForPathAsync(
                _context,
                _aiGeneratorService,
                lesson.Chapter.PathId,
                lesson.Chapter.LearningPath.Language,
                cancellationToken);
            if (hasGoalItemMappingChanges)
            {
                await _context.SaveChangesAsync(cancellationToken);
            }

            return Result<GeneratedQuizSkeletonDto>.Success(new GeneratedQuizSkeletonDto(createdQuizzes));
        }
        catch (Exception ex)
        {
            return Result<GeneratedQuizSkeletonDto>.Failure("QUIZ_GENERATION_FAILED", $"Failed to generate quiz skeleton: {ex.Message}");
        }
    }

    private static int DetermineQuizCount(Lesson lesson)
    {

        var random = new Random(lesson.LessonId.GetHashCode());
        var chapterLessonCount = lesson.Chapter.Lessons?.Count ?? 1;
        var lessonIndex = lesson.OrderIndex;

        if (random.NextDouble() > 0.6)
            return 0;

        if (lessonIndex >= chapterLessonCount / 2)
        {
            if (random.NextDouble() > 0.2)
            {
                return random.NextDouble() > 0.3 ? 2 : 1;
            }
        }

        return random.NextDouble() > 0.5 ? 1 : 0;
    }

    private static string BuildPrompt(Lesson lesson, int quizCount, LanguageSelection language)
    {
        var subject = lesson.Chapter.LearningPath.Subject.Name;
        var chapterTitle = lesson.Chapter.Title;
        var lessonTitle = lesson.Title;

        var languageInstruction = language switch
        {
            LanguageSelection.VietNamese => @"
=== LANGUAGE REQUIREMENTS ===
- Generate ALL content in Vietnamese language
- IMPORTANT: Keep technical terms in English when translating to Vietnamese would cause confusion
- Examples of terms to keep in English: API, REST, JSON, Docker, Algorithm, Framework, etc.
- Use Vietnamese for general descriptions and explanations
",
            LanguageSelection.English => @"
=== LANGUAGE REQUIREMENTS ===
- Generate ALL content in English language
- Use clear, professional English
",
            _ => ""
        };

        return $@"Generate {quizCount} quiz skeleton(s) for a lesson in JSON format.

=== CONTEXT ===
Subject: {subject}
Chapter: {chapterTitle}
Lesson: {lessonTitle}
Lesson Content: {lesson.Content ?? "Content will be generated later"}

{languageInstruction}

=== REQUIREMENTS ===
- Generate exactly {quizCount} quiz(zes)
- Each quiz should focus on different aspects of the lesson
- Quiz titles should be specific and descriptive
- Quiz titles MUST NOT be identical to the lesson title
- Quiz titles MUST stay directly related to the lesson topic
- Quiz descriptions should explain what the quiz tests
- Quizzes should be relevant to the lesson content and learning objectives

=== JSON FORMAT ===
{{
  ""quizzes"": [
    {{
      ""title"": ""Quiz title that describes what it tests"",
      ""description"": ""Brief description of what this quiz covers and its purpose""
    }}
  ]
}}

IMPORTANT: Return ONLY valid JSON. No markdown, no extra text.";
    }

    private class QuizGenerationData
    {
        public List<QuizData> Quizzes { get; set; } = new();
    }

    private class QuizData
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
    }
}
