using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Quizzes.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Quizzes.Commands.SubmitQuizAttempt;

public class SubmitQuizAttemptCommandHandler : IRequestHandler<SubmitQuizAttemptCommand, Result<SubmitQuizResultDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IQuizCacheService _quizCacheService;

    public SubmitQuizAttemptCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IQuizCacheService quizCacheService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _quizCacheService = quizCacheService;
    }

    public async Task<Result<SubmitQuizResultDto>> Handle(SubmitQuizAttemptCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();

        var attempt = await _context.QuizAttempts
            .Include(a => a.Quiz)
                .ThenInclude(q => q.Questions)
            .FirstOrDefaultAsync(a => a.AttemptId == request.AttemptId, cancellationToken);

        if (attempt == null)
            return Result<SubmitQuizResultDto>.Failure("ATTEMPT_NOT_FOUND", "Quiz attempt not found");

        if (attempt.UserId != userId)
            return Result<SubmitQuizResultDto>.Failure("UNAUTHORIZED", "You do not have access to this attempt");

        if (attempt.Status != QuizAttemptStatus.InProgress)
            return Result<SubmitQuizResultDto>.Failure("ATTEMPT_ALREADY_COMPLETED", "This attempt has already been submitted");

        var now = DateTime.UtcNow;
        var timeExpired = IsTimeExpired(attempt.StartTime, attempt.Quiz.TimeLimit);

        var answerMap = request.Answers
            .GroupBy(a => a.QuestionId)
            .ToDictionary(g => g.Key, g => g.First().Answer);

        var questions = attempt.Quiz.Questions.OrderBy(q => q.OrderIndex).ToList();
        var totalPoints = questions.Sum(q => q.Points);
        decimal earnedScore = 0;

        var questionResults = new List<QuestionResultDto>();

        foreach (var question in questions)
        {
            var userAnswer = answerMap.GetValueOrDefault(question.QuestionId, string.Empty);
            var isCorrect = !timeExpired && IsAnswerCorrect(question, userAnswer);
            var earnedPoints = isCorrect ? question.Points : 0;
            earnedScore += earnedPoints;

            questionResults.Add(new QuestionResultDto(
                question.QuestionId,
                question.QuestionText,
                question.Type ?? QuestionType.SingleChoice,
                string.IsNullOrEmpty(question.Options) ? new List<string>() : question.Options.Split("||").ToList(),
                userAnswer,
                question.CorrectAnswer ?? string.Empty,
                isCorrect,
                question.Points,
                earnedPoints
            ));
        }

        var percentage = totalPoints > 0 ? Math.Round(earnedScore / totalPoints * 100, 2) : 0;
        var passed = earnedScore >= (attempt.Quiz.PassingScore ?? 8);

        attempt.EndTime = now;
        attempt.Score = earnedScore;
        attempt.Status = passed ? QuizAttemptStatus.Passed : QuizAttemptStatus.NotPassed;
        attempt.Answers = System.Text.Json.JsonSerializer.Serialize(request.Answers);

        await _context.SaveChangesAsync(cancellationToken);
        await _quizCacheService.InvalidateQuizStatusAsync(attempt.QuizId, userId, cancellationToken);

        return Result<SubmitQuizResultDto>.Success(new SubmitQuizResultDto(
            attempt.AttemptId,
            attempt.QuizId,
            earnedScore,
            totalPoints,
            percentage,
            passed,
            attempt.StartTime,
            attempt.EndTime.Value,
            questionResults
        ));
    }

    private static bool IsTimeExpired(DateTime startTime, int? timeLimitMinutes)
    {
        if (timeLimitMinutes == null || timeLimitMinutes <= 0)
            return false;

        var elapsed = (DateTime.UtcNow - startTime).TotalMinutes;
        return elapsed > timeLimitMinutes.Value;
    }

    private static bool IsAnswerCorrect(Questions question, string userAnswer)
    {
        if (string.IsNullOrWhiteSpace(userAnswer) || string.IsNullOrWhiteSpace(question.CorrectAnswer))
            return false;

        return question.Type switch
        {
            QuestionType.MultipleChoice => CompareMultipleChoice(userAnswer, question.CorrectAnswer),
            QuestionType.Matching => string.Equals(NormalizeAnswer(userAnswer), NormalizeAnswer(question.CorrectAnswer), StringComparison.OrdinalIgnoreCase),
            QuestionType.Ordering => string.Equals(NormalizeAnswer(userAnswer), NormalizeAnswer(question.CorrectAnswer), StringComparison.OrdinalIgnoreCase),
            _ => string.Equals(userAnswer.Trim(), question.CorrectAnswer.Trim(), StringComparison.OrdinalIgnoreCase)
        };
    }

    private static bool CompareMultipleChoice(string userAnswer, string correctAnswer)
    {
        var userParts = userAnswer.Split(',').Select(s => s.Trim().ToLowerInvariant()).OrderBy(s => s).ToList();
        var correctParts = correctAnswer.Split(',').Select(s => s.Trim().ToLowerInvariant()).OrderBy(s => s).ToList();
        return userParts.SequenceEqual(correctParts);
    }

    private static string NormalizeAnswer(string answer)
    {
        return string.Join(",", answer.Split(',').Select(s => s.Trim()));
    }
}
