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
                q.Type ?? QuestionType.MultipleChoice,
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
Generate quiz questions in JSON format based on the lesson content below.

=== CONTEXT ===
Subject: {subject}
Quiz title: {quiz.Title}
Quiz description: {quiz.Description}
Lesson title: {lesson.Title}
Lesson content:
{lesson.Content}

=== REQUIREMENTS ===
- Generate 5 to 10 questions
- Mix of MultipleChoice (type = 0) and TrueFalse (type = 1)
- At least 70% should be MultipleChoice
- MultipleChoice: exactly 4 options, 1 correct answer
- TrueFalse: options should be [""True"", ""False""]
- Questions should test understanding, not just memorization
- Points: 1 for easy, 2 for medium, 3 for hard
- Write questions in the same language as the lesson title

Return ONLY valid JSON (no markdown, no extra text):
{{
  ""questions"": [
    {{
      ""questionText"": ""What is..."",
      ""type"": 0,
      ""options"": [""Option A"", ""Option B"", ""Option C"", ""Option D""],
      ""correctAnswer"": ""Option A"",
      ""points"": 1
    }},
    {{
      ""questionText"": ""True or False: ..."",
      ""type"": 1,
      ""options"": [""True"", ""False""],
      ""correctAnswer"": ""True"",
      ""points"": 1
    }}
  ]
}}";
    }
}
