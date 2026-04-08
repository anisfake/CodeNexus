using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Quizzes.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Quizzes.Commands.GenerateQuizQuestions;

public class GenerateQuizQuestionsCommandHandler : IRequestHandler<GenerateQuizQuestionsCommand, Result<QuizQuestionsDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAIGeneratorService _aiGeneratorService;

    public GenerateQuizQuestionsCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IAIGeneratorService aiGeneratorService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _aiGeneratorService = aiGeneratorService;
    }

    public async Task<Result<QuizQuestionsDto>> Handle(GenerateQuizQuestionsCommand request, CancellationToken cancellationToken)
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
            return Result<QuizQuestionsDto>.Failure("QUIZ_NOT_FOUND", "Quiz not found");

        if (quiz.Lesson == null)
            return Result<QuizQuestionsDto>.Failure("QUIZ_NO_LESSON", "Quiz is not associated with a lesson");

        if (quiz.Lesson.Chapter.LearningPath.UserId != userId)
            return Result<QuizQuestionsDto>.Failure("UNAUTHORIZED", "User not authenticated");

        if (quiz.Questions.Any())
        {
            return Result<QuizQuestionsDto>.Success(MapToDto(quiz));
        }

        try
        {
            var language = quiz.Lesson.Chapter.LearningPath.Language;
            var lessonQuizzes = await _context.Quizzes
                .Include(q => q.Questions)
                .Where(q => q.LessonId == quiz.LessonId && !q.IsDeleted)
                .ToListAsync(cancellationToken);

            var existingQuestionTexts = lessonQuizzes
                .SelectMany(q => q.Questions)
                .Where(q => !q.IsDeleted && !string.IsNullOrWhiteSpace(q.QuestionText))
                .Select(q => q.QuestionText)
                .ToList();

            var prompt = BuildPrompt(quiz, quiz.Lesson, language, existingQuestionTexts);
            var generated = await _aiGeneratorService.GenerateStructureAsync<GeneratedQuestionsDto>(prompt, AIUsageType.ContentGeneration);

            if (generated?.Questions == null || generated.Questions.Count == 0)
                return Result<QuizQuestionsDto>.Failure("INVALID_AI_RESPONSE", "AI returned invalid response.");

            if (HasDuplicateQuestionsInGeneratedSet(generated.Questions))
                return Result<QuizQuestionsDto>.Failure("DUPLICATE_QUESTION", "AI generated duplicate questions in the same quiz set.");

            if (HasDuplicateWithExistingQuestions(generated.Questions, existingQuestionTexts))
                return Result<QuizQuestionsDto>.Failure("DUPLICATE_QUESTION", "AI generated questions duplicated with another quiz in the same lesson.");

            NormalizePoints(generated.Questions);

            var orderIndex = 0;
            foreach (var q in generated.Questions)
            {
                var question = new Questions
                {
                    QuestionId = NewId.NextGuid(),
                    QuizId = quiz.QuizId,
                    QuestionText = q.QuestionText,
                    Type = q.Type,
                    Options = q.Options.Count > 0 ? string.Join("||", q.Options) : null,
                    CorrectAnswer = q.CorrectAnswer,
                    Points = q.Points,
                    OrderIndex = orderIndex++
                };

                await _context.Questions.AddAsync(question, cancellationToken);
            }

            quiz.TimeLimit = Math.Clamp(generated.TimeLimitMinutes, 6, 10);
            quiz.PassingScore = 8;

            await _context.SaveChangesAsync(cancellationToken);

            var savedQuiz = await _context.Quizzes
                    .Include(q => q.Questions)
                .FirstAsync(q => q.QuizId == request.QuizId, cancellationToken);

            return Result<QuizQuestionsDto>.Success(MapToDto(savedQuiz));
        }
        catch (Exception ex)
        {
            return Result<QuizQuestionsDto>.Failure("QUESTION_GENERATION_FAILED",
                $"Failed to generate quiz questions: {ex.Message}");
        }
    }

    private static QuizQuestionsDto MapToDto(Quiz quiz)
    {
        var questions = quiz.Questions
            .OrderBy(q => q.OrderIndex)
            .Select(q => new QuestionItemDto(
                q.QuestionId,
                q.QuestionText,
                q.Type ?? QuestionType.SingleChoice,
                string.IsNullOrEmpty(q.Options) ? new List<string>() : q.Options.Split("||").ToList(),
                q.CorrectAnswer ?? string.Empty,
                q.Points,
                q.OrderIndex ?? 0
            ))
            .ToList();

        return new QuizQuestionsDto(quiz.QuizId, quiz.Title, quiz.TimeLimit, quiz.PassingScore, questions);
    }

    private static void NormalizePoints(List<GeneratedQuestionDto> questions)
    {
        const decimal totalTarget = 10m;
        var currentTotal = questions.Sum(q => q.Points);

        if (currentTotal == totalTarget)
            return;

        var scale = totalTarget / currentTotal;
        decimal runningSum = 0;

        for (int i = 0; i < questions.Count; i++)
        {
            decimal normalized;
            if (i == questions.Count - 1)
            {
                normalized = totalTarget - runningSum;
            }
            else
            {
                normalized = Math.Round(questions[i].Points * scale, 1);
            }

            runningSum += normalized;
            questions[i] = questions[i] with { Points = normalized };
        }
    }

    private static bool HasDuplicateQuestionsInGeneratedSet(List<GeneratedQuestionDto> generatedQuestions)
    {
        var unique = new HashSet<string>();

        foreach (var question in generatedQuestions)
        {
            var normalized = NormalizeText(question.QuestionText);
            if (string.IsNullOrWhiteSpace(normalized))
                continue;

            if (!unique.Add(normalized))
                return true;
        }

        return false;
    }

    private static bool HasDuplicateWithExistingQuestions(List<GeneratedQuestionDto> generatedQuestions, List<string> existingQuestionTexts)
    {
        var existing = existingQuestionTexts
            .Select(NormalizeText)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet();

        return generatedQuestions
            .Select(q => NormalizeText(q.QuestionText))
            .Any(normalized => !string.IsNullOrWhiteSpace(normalized) && existing.Contains(normalized));
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

    private static string BuildPrompt(Quiz quiz, Lesson lesson, LanguageSelection language, List<string> existingQuestionTexts)
    {
        var subject = lesson.Chapter.LearningPath.Subject.Name;
        var existingQuestionsInstruction = existingQuestionTexts.Count == 0
            ? "- No existing questions in this lesson"
            : $"- Existing questions in this lesson (must avoid duplicates):\n- {string.Join("\n- ", existingQuestionTexts.Take(20))}";

        var languageInstruction = language switch
        {
            LanguageSelection.VietNamese => @"
=== LANGUAGE REQUIREMENTS ===
- Generate ALL content in Vietnamese language
- IMPORTANT: Keep technical terms in English when translating to Vietnamese would cause confusion or change meaning
- Examples of terms to keep in English: API, REST, JSON, Docker, Kubernetes, Framework, Library, Algorithm, etc.
- Use Vietnamese for general descriptions and explanations
- Example: ""Viết hàm sắp xếp bubble sort"" (correct) instead of ""Viết hàm sắp xếp bong bóng"" (wrong)
",
            LanguageSelection.English => @"
=== LANGUAGE REQUIREMENTS ===
- Generate ALL content in English language
- Use clear, professional English
",
            _ => ""
        };

        return $@"You are a senior {subject} instructor.
Generate exactly 6 quiz questions in JSON format, one for each question type.

=== CONTEXT ===
Subject: {subject}
Quiz title: {quiz.Title}
Quiz description: {quiz.Description}
Lesson title: {lesson.Title}
Lesson content:
{lesson.Content}

{languageInstruction}

=== QUESTION TYPES (generate exactly 1 of each) ===
1. TrueFalse (type = 0): A statement that is either true or false.
   - options: [""True"", ""False""]
   - correctAnswer: ""True"" or ""False""

2. MultipleChoice (type = 1): A question with multiple correct answers.
   - options: exactly 4 options
   - correctAnswer: comma-separated correct answers, e.g. ""Option A, Option C""
   - Can include a code snippet in questionText and ask what the code does, what it outputs, or which statements about it are correct

3. SingleChoice (type = 2): A question with exactly 1 correct answer.
   - options: exactly 4 options
   - correctAnswer: the single correct option text
   - Can include a code snippet in questionText and ask to analyze it, predict output, or identify an issue

4. Matching (type = 3): Match left items to right items.
   - options: pairs formatted as ""Left1::Right1"", ""Left2::Right2"", etc. (4 pairs)
   - correctAnswer: same pairs in correct order, joined by "","", e.g. ""Left1::Right1,Left2::Right2,Left3::Right3,Left4::Right4""

5. FillInTheBlank (type = 4): A sentence with a blank (use ___ for the blank).
   - options: [] (empty array)
   - correctAnswer: the word/phrase that fills the blank

6. Ordering (type = 5): Put items in the correct order.
   - options: exactly 5 items in SHUFFLED order
   - correctAnswer: the same 5 items in CORRECT order, joined by "","", e.g. ""Step1,Step2,Step3,Step4,Step5""

=== REQUIREMENTS ===
- Generate EXACTLY 6 questions, one per type above
- Questions MUST be relevant to the quiz title (""{quiz.Title}"") and quiz description (""{quiz.Description}"")
- Use the lesson content as knowledge source
- Questions MUST NOT duplicate each other within this generated set
- Questions MUST NOT duplicate existing questions in other quizzes of the same lesson
- For MultipleChoice and SingleChoice: prefer questions that involve analyzing a code snippet, predicting output, or reasoning about code behavior — not just recalling definitions
- For code snippets in questions: use \n for newlines inside the questionText string
- Points: distribute points across all 6 questions so they sum to EXACTLY 10. Use decimal values (e.g., 1.0, 1.5, 2.0, 2.5). Assign higher points to harder questions.
- timeLimitMinutes: an integer between 6 and 10 representing the total quiz duration in minutes. Choose based on the overall difficulty of the questions.

=== EXISTING QUESTIONS IN THIS LESSON ===
{existingQuestionsInstruction}

Return ONLY valid JSON (no markdown, no extra text):
{{
  ""timeLimitMinutes"": 8,
  ""questions"": [
    {{
      ""questionText"": ""Python is a compiled language."",
      ""type"": 0,
      ""options"": [""True"", ""False""],
      ""correctAnswer"": ""False"",
      ""points"": 1.0
    }},
    {{
      ""questionText"": ""Given the following code:\nx = [1, 2, 3]\ny = x\ny.append(4)\nWhich statements are true?"",
      ""type"": 1,
      ""options"": [""x equals [1, 2, 3, 4]"", ""y equals [1, 2, 3, 4]"", ""x and y refer to the same object"", ""x equals [1, 2, 3]""],
      ""correctAnswer"": ""x equals [1, 2, 3, 4], y equals [1, 2, 3, 4], x and y refer to the same object"",
      ""points"": 2.0
    }},
    {{
      ""questionText"": ""What is the output of this code?\nfor i in range(3):\n    print(i, end=' ')"",
      ""type"": 2,
      ""options"": [""1 2 3"", ""0 1 2"", ""0 1 2 3"", ""1 2 3 4""],
      ""correctAnswer"": ""0 1 2"",
      ""points"": 2.0
    }},
    {{
      ""questionText"": ""Match each data type with its example:"",
      ""type"": 3,
      ""options"": [""int::42"", ""str::hello"", ""float::3.14"", ""bool::True""],
      ""correctAnswer"": ""int::42,str::hello,float::3.14,bool::True"",
      ""points"": 1.5
    }},
    {{
      ""questionText"": ""The keyword ___ is used to create a loop that iterates over a sequence in Python."",
      ""type"": 4,
      ""options"": [],
      ""correctAnswer"": ""for"",
      ""points"": 1.0
    }},
    {{
      ""questionText"": ""Arrange the steps to read a file and process its content in Python:"",
      ""type"": 5,
      ""options"": [""Close the file"", ""Open the file with open()"", ""Process each line"", ""Read the content"", ""Import necessary modules""],
      ""correctAnswer"": ""Import necessary modules,Open the file with open(),Read the content,Process each line,Close the file"",
      ""points"": 2.5
    }}
  ]
}}";
    }
}

