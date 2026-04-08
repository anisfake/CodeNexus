using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Quizzes.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Quizzes.Commands.GenerateSingleQuizSkeleton;

public class GenerateSingleQuizSkeletonCommandHandler : IRequestHandler<GenerateSingleQuizSkeletonCommand, Result<QuizSkeletonDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAIGeneratorService _aiGeneratorService;

    public GenerateSingleQuizSkeletonCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IAIGeneratorService aiGeneratorService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _aiGeneratorService = aiGeneratorService;
    }

    public async Task<Result<QuizSkeletonDto>> Handle(GenerateSingleQuizSkeletonCommand request, CancellationToken cancellationToken)
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
                return Result<QuizSkeletonDto>.Failure("LESSON_NOT_FOUND", "Lesson not found");

            if (lesson.Chapter.LearningPath.UserId != userId)
                return Result<QuizSkeletonDto>.Failure("UNAUTHORIZED", "User not authenticated");

            if (string.IsNullOrWhiteSpace(lesson.Title))
                return Result<QuizSkeletonDto>.Failure("LESSON_TITLE_REQUIRED", "Lesson title is required to generate quiz skeleton");

            if (string.IsNullOrWhiteSpace(lesson.Content))
                return Result<QuizSkeletonDto>.Failure("LESSON_CONTENT_REQUIRED", "Lesson content is required to generate quiz skeleton");

            var language = lesson.Chapter.LearningPath.Language;
            var prompt = BuildPrompt(lesson, language);
            var generatedData = await _aiGeneratorService.GenerateStructureAsync<QuizGenerationData>(prompt, AIUsageType.StructureGeneration);

            var quizData = generatedData?.Quizzes?.FirstOrDefault();
            if (quizData == null || string.IsNullOrWhiteSpace(quizData.Title))
                return Result<QuizSkeletonDto>.Failure("INVALID_AI_RESPONSE", "AI returned invalid response.");

            var hasDuplicateTitle = lesson.Quizzes
                .Where(q => !q.IsDeleted)
                .Any(q => Normalize(q.Title) == Normalize(quizData.Title));

            if (hasDuplicateTitle)
                return Result<QuizSkeletonDto>.Failure("DUPLICATE_QUIZ_SKELETON", "Generated quiz skeleton is too similar to an existing quiz");

            var quiz = new Quiz
            {
                QuizId = NewId.NextGuid(),
                LessonId = lesson.LessonId,
                Title = quizData.Title.Trim(),
                Description = quizData.Description?.Trim(),
                TimeLimit = null,
                PassingScore = null,
                CreatedAt = DateTime.UtcNow
            };

            await _context.Quizzes.AddAsync(quiz, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            var dto = new QuizSkeletonDto(
                quiz.QuizId,
                quiz.Title,
                quiz.Description,
                quiz.TimeLimit,
                quiz.PassingScore
            );

            return Result<QuizSkeletonDto>.Success(dto);
        }
        catch (Exception ex)
        {
            return Result<QuizSkeletonDto>.Failure("QUIZ_GENERATION_FAILED", $"Failed to generate single quiz skeleton: {ex.Message}");
        }
    }

    private static string BuildPrompt(Lesson lesson, LanguageSelection language)
    {
        var learningPath = lesson.Chapter.LearningPath;
        var subject = learningPath.Subject.Name;
        var pathTitle = learningPath.Title;
        var chapterTitle = lesson.Chapter.Title;
        var lessonTitle = lesson.Title;
        var existingQuizTitles = lesson.Quizzes
            .Where(q => !q.IsDeleted && !string.IsNullOrWhiteSpace(q.Title))
            .Select(q => q.Title.Trim())
            .Distinct()
            .ToList();
        var existingQuizzesInstruction = existingQuizTitles.Count == 0
            ? "- No existing quiz skeletons yet."
            : $"- Existing quiz skeleton titles (MUST avoid duplicates and same topic):\n- {string.Join("\n- ", existingQuizTitles)}";

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

        return $@"Generate 1 quiz skeleton for a lesson in JSON format.

=== CONTEXT ===
Subject: {subject}
Learning Path: {pathTitle}
Learning Path Description: {learningPath.Description ?? "N/A"}
Chapter: {chapterTitle}
Lesson: {lessonTitle}
Lesson Content: {lesson.Content ?? "Content will be generated later"}

{languageInstruction}

=== REQUIREMENTS ===
- Generate exactly 1 quiz
- This is skeleton only, DO NOT generate questions
- Quiz title should be specific and descriptive
- Quiz description should explain what this quiz tests
- Quiz should be relevant to lesson content and learning objectives
- Quiz topic must be different from existing quiz skeletons for this lesson
- Avoid generating another quiz with the same title or the same main topic
- Return data for quiz metadata only (title + description)

=== EXISTING QUIZZES ===
{existingQuizzesInstruction}

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

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return new string(value
            .Trim()
            .ToLowerInvariant()
            .Where(c => !char.IsWhiteSpace(c))
            .ToArray());
    }

    private class QuizGenerationData
    {
        public List<QuizData> Quizzes { get; set; } = new();
    }

    private class QuizData
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}
