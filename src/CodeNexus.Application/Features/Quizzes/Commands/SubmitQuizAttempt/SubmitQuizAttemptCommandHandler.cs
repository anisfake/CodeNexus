using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Helpers;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Quizzes.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.RegularExpressions;

namespace CodeNexus.Application.Features.Quizzes.Commands.SubmitQuizAttempt;

public class SubmitQuizAttemptCommandHandler : IRequestHandler<SubmitQuizAttemptCommand, Result<SubmitQuizResultDto>>
{
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex OptionPrefixRegex = new(@"^\s*[A-Da-d][\)\.\:\-]\s*", RegexOptions.Compiled);

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public SubmitQuizAttemptCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<SubmitQuizResultDto>> Handle(SubmitQuizAttemptCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();

        var attempt = await _context.QuizAttempts
            .Include(a => a.Quiz)
                .ThenInclude(q => q.Lesson)
                .ThenInclude(l => l.Chapter)
            .Include(a => a.Quiz)
                .ThenInclude(q => q.Questions)
            .FirstOrDefaultAsync(a => a.AttemptId == request.AttemptId, cancellationToken);

        if (attempt == null)
            return Result<SubmitQuizResultDto>.Failure("ATTEMPT_NOT_FOUND", "Quiz attempt not found");

        if (attempt.UserId != userId)
            return Result<SubmitQuizResultDto>.Failure("UNAUTHORIZED", "User not authenticated");

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

        await UpsertDailyCheckinForQuizAsync(userId, passed, percentage, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        if (attempt.Quiz.Lesson?.Chapter != null)
        {
            var hasChapterCompletionChanges = await ChapterCompletionSyncHelper.SyncAsync(
                _context,
                attempt.Quiz.Lesson.Chapter.ChapterId,
                userId,
                cancellationToken);

            var hasGoalProgressChanges = await UserGoalProgressSyncHelper.SyncForLearningPathAsync(
                _context,
                attempt.Quiz.Lesson.Chapter.PathId,
                userId,
                cancellationToken);

            if (hasGoalProgressChanges || hasChapterCompletionChanges)
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

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
            QuestionType.Matching => string.Equals(NormalizeDelimitedAnswer(userAnswer), NormalizeDelimitedAnswer(question.CorrectAnswer), StringComparison.OrdinalIgnoreCase),
            QuestionType.Ordering => string.Equals(NormalizeDelimitedAnswer(userAnswer), NormalizeDelimitedAnswer(question.CorrectAnswer), StringComparison.OrdinalIgnoreCase),
            _ => string.Equals(NormalizeComparableText(userAnswer), NormalizeComparableText(question.CorrectAnswer), StringComparison.OrdinalIgnoreCase)
        };
    }

    private static bool CompareMultipleChoice(string userAnswer, string correctAnswer)
    {
        var userParts = userAnswer
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeComparableText)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var correctParts = correctAnswer
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeComparableText)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return userParts.SequenceEqual(correctParts, StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeDelimitedAnswer(string answer)
    {
        return string.Join(",", answer
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeComparableText));
    }

    private static string NormalizeComparableText(string answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
            return string.Empty;

        var normalized = answer
            .Normalize(NormalizationForm.FormKC)
            .Replace('\u00A0', ' ')
            .Trim();

        normalized = OptionPrefixRegex.Replace(normalized, string.Empty);
        return WhitespaceRegex.Replace(normalized, " ");
    }

    private async Task UpsertDailyCheckinForQuizAsync(
        Guid userId,
        bool passed,
        decimal percentage,
        CancellationToken cancellationToken)
    {
        var today = VietnamDateTimeHelper.GetTodayDate();
        var existing = await _context.DailyCheckins
            .FirstOrDefaultAsync(x => x.UserId == userId && x.CheckinDate == today, cancellationToken);

        if (existing == null)
        {
            _context.DailyCheckins.Add(new DailyCheckins
            {
                CheckinId = NewId.NextGuid(),
                UserId = userId,
                CheckinDate = today,
                Productivity = DailyCheckinEvaluationHelper.QuizActivityIncrement,
                CreatedAt = DateTime.UtcNow
            });
            return;
        }

        existing.Productivity = DailyCheckinEvaluationHelper.IncrementActivityCount(
            existing.Productivity, DailyCheckinEvaluationHelper.QuizActivityIncrement);
    }
}
