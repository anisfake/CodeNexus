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
            return Result<QuizQuestionsDto>.Failure("UNAUTHORIZED", "You do not have access to this quiz");

        if (quiz.Questions.Any())
        {
            return Result<QuizQuestionsDto>.Success(MapToDto(quiz));
        }

        try
        {
            var prompt = BuildPrompt(quiz, quiz.Lesson);
            var generated = await _aiGeneratorService.GenerateStructureAsync<GeneratedQuestionsDto>(prompt);

            if (generated?.Questions == null || generated.Questions.Count == 0)
                return Result<QuizQuestionsDto>.Failure("INVALID_AI_RESPONSE", "AI returned no questions");

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

        return new QuizQuestionsDto(quiz.QuizId, quiz.Title, questions);
    }

    private static string BuildPrompt(Quiz quiz, Lesson lesson)
    {
        var subject = lesson.Chapter.LearningPath.Subject.Name;

        return $@"You are a senior {subject} instructor.
Generate exactly 6 quiz questions in JSON format, one for each question type.

=== CONTEXT ===
Subject: {subject}
Quiz title: {quiz.Title}
Quiz description: {quiz.Description}
Lesson title: {lesson.Title}
Lesson content:
{lesson.Content}

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
- For MultipleChoice and SingleChoice: prefer questions that involve analyzing a code snippet, predicting output, or reasoning about code behavior — not just recalling definitions
- For code snippets in questions: use \n for newlines inside the questionText string
- Points: 1 for easy, 2 for medium, 3 for hard
- Write questions in the same language as the lesson title

Return ONLY valid JSON (no markdown, no extra text):
{{
  ""questions"": [
    {{
      ""questionText"": ""Python is a compiled language."",
      ""type"": 0,
      ""options"": [""True"", ""False""],
      ""correctAnswer"": ""False"",
      ""points"": 1
    }},
    {{
      ""questionText"": ""Given the following code:\nx = [1, 2, 3]\ny = x\ny.append(4)\nWhich statements are true?"",
      ""type"": 1,
      ""options"": [""x equals [1, 2, 3, 4]"", ""y equals [1, 2, 3, 4]"", ""x and y refer to the same object"", ""x equals [1, 2, 3]""],
      ""correctAnswer"": ""x equals [1, 2, 3, 4], y equals [1, 2, 3, 4], x and y refer to the same object"",
      ""points"": 2
    }},
    {{
      ""questionText"": ""What is the output of this code?\nfor i in range(3):\n    print(i, end=' ')"",
      ""type"": 2,
      ""options"": [""1 2 3"", ""0 1 2"", ""0 1 2 3"", ""1 2 3 4""],
      ""correctAnswer"": ""0 1 2"",
      ""points"": 2
    }},
    {{
      ""questionText"": ""Match each data type with its example:"",
      ""type"": 3,
      ""options"": [""int::42"", ""str::hello"", ""float::3.14"", ""bool::True""],
      ""correctAnswer"": ""int::42,str::hello,float::3.14,bool::True"",
      ""points"": 2
    }},
    {{
      ""questionText"": ""The keyword ___ is used to create a loop that iterates over a sequence in Python."",
      ""type"": 4,
      ""options"": [],
      ""correctAnswer"": ""for"",
      ""points"": 1
    }},
    {{
      ""questionText"": ""Arrange the steps to read a file and process its content in Python:"",
      ""type"": 5,
      ""options"": [""Close the file"", ""Open the file with open()"", ""Process each line"", ""Read the content"", ""Import necessary modules""],
      ""correctAnswer"": ""Import necessary modules,Open the file with open(),Read the content,Process each line,Close the file"",
      ""points"": 3
    }}
  ]
}}";
    }
}
