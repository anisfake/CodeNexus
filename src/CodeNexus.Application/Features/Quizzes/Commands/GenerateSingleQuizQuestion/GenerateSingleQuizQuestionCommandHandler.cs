using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Quizzes.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Quizzes.Commands.GenerateSingleQuizQuestion;

public class GenerateSingleQuizQuestionCommandHandler : IRequestHandler<GenerateSingleQuizQuestionCommand, Result<QuestionItemDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAIGeneratorService _aiGeneratorService;

    public GenerateSingleQuizQuestionCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IAIGeneratorService aiGeneratorService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _aiGeneratorService = aiGeneratorService;
    }

    public async Task<Result<QuestionItemDto>> Handle(GenerateSingleQuizQuestionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.GetUserId();

            var quiz = await _context.Quizzes
                .Include(q => q.Lesson)
                    .ThenInclude(l => l!.Chapter)
                        .ThenInclude(c => c.LearningPath)
                            .ThenInclude(lp => lp.Subject)
                .Include(q => q.Questions)
                .FirstOrDefaultAsync(q => q.QuizId == request.QuizId, cancellationToken);

            if (quiz == null)
                return Result<QuestionItemDto>.Failure("QUIZ_NOT_FOUND", "Quiz not found");

            if (quiz.Lesson == null)
                return Result<QuestionItemDto>.Failure("QUIZ_NO_LESSON", "Quiz is not associated with a lesson");

            if (quiz.Lesson.Chapter.LearningPath.UserId != userId)
                return Result<QuestionItemDto>.Failure("UNAUTHORIZED", "User not authenticated");

            var lesson = quiz.Lesson;

            var lessonQuizzes = await _context.Quizzes
                .Include(q => q.Questions)
                .Where(q => q.LessonId == lesson.LessonId && !q.IsDeleted)
                .ToListAsync(cancellationToken);

            var existingQuestionTexts = lessonQuizzes
                .SelectMany(q => q.Questions)
                .Where(q => !q.IsDeleted && !string.IsNullOrWhiteSpace(q.QuestionText))
                .Select(q => q.QuestionText)
                .ToList();

            var prompt = BuildPrompt(quiz, lesson, lessonQuizzes, existingQuestionTexts, lesson.Chapter.LearningPath.Language, request.QuestionType);
            var generated = await _aiGeneratorService.GenerateStructureAsync<GeneratedQuestionsDto>(prompt, AIUsageType.ContentGeneration);

            var generatedQuestion = generated?.Questions?.FirstOrDefault();
            if (generatedQuestion == null || string.IsNullOrWhiteSpace(generatedQuestion.QuestionText))
                return Result<QuestionItemDto>.Failure("INVALID_AI_RESPONSE", "AI returned invalid response.");

            if (generatedQuestion.Type != request.QuestionType)
                return Result<QuestionItemDto>.Failure("QUESTION_TYPE_MISMATCH", "AI generated a different question type than requested.");

            if (IsDuplicateQuestion(generatedQuestion.QuestionText, existingQuestionTexts))
                return Result<QuestionItemDto>.Failure("DUPLICATE_QUESTION", "Generated question is duplicated with existing questions in this lesson.");

            var maxOrderIndex = quiz.Questions
                .Where(q => !q.IsDeleted)
                .Select(q => q.OrderIndex ?? 0)
                .DefaultIfEmpty(-1)
                .Max();

            var question = new Questions
            {
                QuestionId = NewId.NextGuid(),
                QuizId = quiz.QuizId,
                QuestionText = generatedQuestion.QuestionText.Trim(),
                Type = generatedQuestion.Type,
                Options = generatedQuestion.Options.Count > 0 ? string.Join("||", generatedQuestion.Options) : null,
                CorrectAnswer = generatedQuestion.CorrectAnswer,
                Points = generatedQuestion.Points <= 0 ? 1 : generatedQuestion.Points,
                OrderIndex = maxOrderIndex + 1
            };

            await _context.Questions.AddAsync(question, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            var dto = new QuestionItemDto(
                question.QuestionId,
                question.QuestionText,
                question.Type ?? QuestionType.SingleChoice,
                string.IsNullOrWhiteSpace(question.Options) ? new List<string>() : question.Options.Split("||").ToList(),
                question.CorrectAnswer ?? string.Empty,
                question.Points,
                question.OrderIndex ?? 0
            );

            return Result<QuestionItemDto>.Success(dto);
        }
        catch (Exception ex)
        {
            return Result<QuestionItemDto>.Failure("QUESTION_GENERATION_FAILED", $"Failed to generate single question: {ex.Message}");
        }
    }

    private static bool IsDuplicateQuestion(string generatedQuestionText, List<string> existingQuestionTexts)
    {
        var normalizedGenerated = NormalizeText(generatedQuestionText);

        return existingQuestionTexts
            .Select(NormalizeText)
            .Any(x => x == normalizedGenerated);
    }

    private static string NormalizeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var filtered = text
            .Trim()
            .ToLowerInvariant()
            .Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
            .ToArray();

        return string.Join(' ', new string(filtered)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string BuildPrompt(
        Quiz quiz,
        Lesson lesson,
        List<Quiz> lessonQuizzes,
        List<string> existingQuestionTexts,
        LanguageSelection language,
        QuestionType requestedQuestionType)
    {
        var subject = lesson.Chapter.LearningPath.Subject.Name;
        var existingQuizTitles = lessonQuizzes
            .Where(q => !string.IsNullOrWhiteSpace(q.Title))
            .Select(q => q.Title.Trim())
            .Distinct()
            .ToList();

        var quizTitlesText = existingQuizTitles.Count == 0
            ? "- No quiz skeletons found in this lesson"
            : $"- {string.Join("\n- ", existingQuizTitles)}";

        var existingQuestionsText = existingQuestionTexts.Count == 0
            ? "- No existing questions"
            : $"- {string.Join("\n- ", existingQuestionTexts.Take(20))}";

        var languageInstruction = language switch
        {
            LanguageSelection.VietNamese => @"
=== LANGUAGE REQUIREMENTS ===
- Generate ALL content in Vietnamese language
- IMPORTANT: Keep technical terms in English when translating to Vietnamese would cause confusion or change meaning
",
            LanguageSelection.English => @"
=== LANGUAGE REQUIREMENTS ===
- Generate ALL content in English language
",
            _ => ""
        };

        var requestedTypeLabel = GetQuestionTypeLabel(requestedQuestionType);
        var typeSpecificRules = GetTypeSpecificRules(requestedQuestionType);
        var typeSpecificJsonExample = GetTypeSpecificJsonExample(requestedQuestionType);

        return $@"You are a senior {subject} instructor.
Generate exactly 1 quiz question in JSON format.

=== CONTEXT ===
Subject: {subject}
Learning path: {lesson.Chapter.LearningPath.Title}
Chapter: {lesson.Chapter.Title}
Lesson title: {lesson.Title}
Lesson content:
{lesson.Content}
Current quiz title: {quiz.Title}
Current quiz description: {quiz.Description}

{languageInstruction}

=== OTHER QUIZ SKELETONS IN THIS LESSON ===
{quizTitlesText}

=== EXISTING QUESTIONS IN THIS LESSON (ALL QUIZZES) ===
{existingQuestionsText}

=== REQUIREMENTS ===
- Generate EXACTLY 1 question for the CURRENT quiz (""{quiz.Title}"")
- The question MUST align with lesson content and current quiz scope
- The question MUST NOT duplicate any existing question in the current quiz or other quizzes in this lesson
- Avoid overlapping same concept already asked by existing questions
- MUST use this requested type: {requestedTypeLabel} ({(int)requestedQuestionType})
- points must be > 0

=== TYPE-SPECIFIC RULES ===
{typeSpecificRules}

=== JSON FORMAT ===
{{
  ""timeLimitMinutes"": 8,
  ""questions"": [
    {typeSpecificJsonExample}
  ]
}}

IMPORTANT: Return ONLY valid JSON. No markdown, no extra text.";
    }

    private static string GetQuestionTypeLabel(QuestionType type)
    {
        return type switch
        {
            QuestionType.TrueFalse => "TrueFalse",
            QuestionType.MultipleChoice => "MultipleChoice",
            QuestionType.SingleChoice => "SingleChoice",
            QuestionType.Matching => "Matching",
            QuestionType.FillInTheBlank => "FillInTheBlank",
            QuestionType.Ordering => "Ordering",
            _ => "SingleChoice"
        };
    }

    private static string GetTypeSpecificRules(QuestionType type)
    {
        return type switch
        {
            QuestionType.TrueFalse => "- type must be 0\n- options must be exactly [\"True\", \"False\"]\n- correctAnswer must be \"True\" or \"False\"",
            QuestionType.MultipleChoice => "- type must be 1\n- options must have exactly 4 items\n- correctAnswer must be comma-separated option texts for all correct options",
            QuestionType.SingleChoice => "- type must be 2\n- options must have exactly 4 items\n- correctAnswer must be exactly one option text",
            QuestionType.Matching => "- type must be 3\n- options must contain exactly 4 pairs in format \"Left::Right\"\n- correctAnswer must list correct 4 pairs joined by comma",
            QuestionType.FillInTheBlank => "- type must be 4\n- questionText must contain ___ blank\n- options must be []\n- correctAnswer must be the exact fill text",
            QuestionType.Ordering => "- type must be 5\n- options must contain exactly 5 SHUFFLED items\n- correctAnswer must contain those 5 items in correct order joined by comma",
            _ => "- type must be 2\n- options must have exactly 4 items\n- correctAnswer must be exactly one option text"
        };
    }

    private static string GetTypeSpecificJsonExample(QuestionType type)
    {
        return type switch
        {
            QuestionType.TrueFalse => "{\n      \"questionText\": \"C# supports method overloading.\",\n      \"type\": 0,\n      \"options\": [\"True\", \"False\"],\n      \"correctAnswer\": \"True\",\n      \"points\": 1.0\n    }",
            QuestionType.MultipleChoice => "{\n      \"questionText\": \"Which statements about foreach in C# are correct?\",\n      \"type\": 1,\n      \"options\": [\"Iterates over collection\", \"Needs index variable\", \"Can be used with arrays\", \"Always modifies source\"],\n      \"correctAnswer\": \"Iterates over collection, Can be used with arrays\",\n      \"points\": 1.5\n    }",
            QuestionType.SingleChoice => "{\n      \"questionText\": \"Which keyword declares a class in C#?\",\n      \"type\": 2,\n      \"options\": [\"class\", \"struct\", \"interface\", \"enum\"],\n      \"correctAnswer\": \"class\",\n      \"points\": 1.5\n    }",
            QuestionType.Matching => "{\n      \"questionText\": \"Match C# type with example value\",\n      \"type\": 3,\n      \"options\": [\"int::42\", \"string::hello\", \"bool::true\", \"double::3.14\"],\n      \"correctAnswer\": \"int::42,string::hello,bool::true,double::3.14\",\n      \"points\": 2.0\n    }",
            QuestionType.FillInTheBlank => "{\n      \"questionText\": \"The keyword ___ is used to inherit a class in C#.\",\n      \"type\": 4,\n      \"options\": [],\n      \"correctAnswer\": \":\",\n      \"points\": 1.0\n    }",
            QuestionType.Ordering => "{\n      \"questionText\": \"Arrange steps to create and use a List in C#\",\n      \"type\": 5,\n      \"options\": [\"Add item\", \"Create new List<int>()\", \"Import System.Collections.Generic\", \"Iterate with foreach\", \"Read Count\"],\n      \"correctAnswer\": \"Import System.Collections.Generic,Create new List<int>(),Add item,Read Count,Iterate with foreach\",\n      \"points\": 2.0\n    }",
            _ => "{\n      \"questionText\": \"Which keyword declares a class in C#?\",\n      \"type\": 2,\n      \"options\": [\"class\", \"struct\", \"interface\", \"enum\"],\n      \"correctAnswer\": \"class\",\n      \"points\": 1.5\n    }"
        };
    }
}
