using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Quizzes.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Quizzes.Commands.StartQuizAttempt;

public class StartQuizAttemptCommandHandler : IRequestHandler<StartQuizAttemptCommand, Result<StartQuizAttemptDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public StartQuizAttemptCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<StartQuizAttemptDto>> Handle(StartQuizAttemptCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();

        var quiz = await _context.Quizzes
                .Include(q => q.Lesson)
                    .ThenInclude(l => l!.Chapter)
                        .ThenInclude(c => c.LearningPath)
                .Include(q => q.Questions)
            .FirstOrDefaultAsync(q => q.QuizId == request.QuizId && !q.IsDeleted, cancellationToken);

        if (quiz == null)
            return Result<StartQuizAttemptDto>.Failure("QUIZ_NOT_FOUND", "Quiz not found");

        if (quiz.Lesson == null)
            return Result<StartQuizAttemptDto>.Failure("QUIZ_NO_LESSON", "Quiz is not associated with a lesson");

        if (quiz.Lesson.Chapter.LearningPath.UserId != userId)
            return Result<StartQuizAttemptDto>.Failure("UNAUTHORIZED", "You do not have access to this quiz");

        if (!quiz.Questions.Any())
            return Result<StartQuizAttemptDto>.Failure("QUIZ_NO_QUESTIONS", "Quiz has no questions. Generate questions first.");

        if (quiz.TimeLimit == null || quiz.PassingScore == null)
        {
            var questions = quiz.Questions.ToList();
            quiz.TimeLimit ??= CalculateTimeLimit(questions);
            quiz.PassingScore ??= CalculatePassingScore(questions);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var existingAttempt = await _context.QuizAttempts
            .FirstOrDefaultAsync(a => a.QuizId == request.QuizId
                && a.UserId == userId
                && a.Status == QuizAttemptStatus.InProgress, cancellationToken);

        if (existingAttempt != null)
        {
            var remaining = CalculateRemainingSeconds(existingAttempt.StartTime, quiz.TimeLimit);

            if (remaining <= 0)
            {
                existingAttempt.Status = QuizAttemptStatus.NotPassed;
                existingAttempt.EndTime = existingAttempt.StartTime.AddMinutes(quiz.TimeLimit ?? 0);
                existingAttempt.Score = 0;
                await _context.SaveChangesAsync(cancellationToken);

                return Result<StartQuizAttemptDto>.Failure("ATTEMPT_TIME_EXPIRED", "Your previous attempt has expired. You can start a new one.");
            }

            return Result<StartQuizAttemptDto>.Success(MapToDto(quiz, existingAttempt, remaining));
        }

        var attempt = new QuizAttempt
        {
            AttemptId = NewId.NextGuid(),
            QuizId = quiz.QuizId,
            UserId = userId,
            StartTime = DateTime.UtcNow,
            Status = QuizAttemptStatus.InProgress
        };

        await _context.QuizAttempts.AddAsync(attempt, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        var totalSeconds = (quiz.TimeLimit ?? 0) * 60;
        return Result<StartQuizAttemptDto>.Success(MapToDto(quiz, attempt, totalSeconds));
    }

    private static int CalculateRemainingSeconds(DateTime startTime, int? timeLimitMinutes)
    {
        if (timeLimitMinutes == null || timeLimitMinutes <= 0)
            return int.MaxValue;

        var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
        var totalSeconds = timeLimitMinutes.Value * 60;
        return Math.Max(0, (int)(totalSeconds - elapsed));
    }

    private static StartQuizAttemptDto MapToDto(Quiz quiz, QuizAttempt attempt, int remainingSeconds)
    {
        var questions = quiz.Questions
            .OrderBy(q => q.OrderIndex)
            .Select(q => new AttemptQuestionDto(
                q.QuestionId,
                q.QuestionText,
                q.Type ?? QuestionType.SingleChoice,
                string.IsNullOrEmpty(q.Options) ? new List<string>() : q.Options.Split("||").ToList(),
                q.Points,
                q.OrderIndex ?? 0
            ))
            .ToList();

        return new StartQuizAttemptDto(
            attempt.AttemptId,
            quiz.QuizId,
            quiz.Title,
            quiz.TimeLimit,
            quiz.PassingScore,
            remainingSeconds,
            attempt.StartTime,
            questions
        );
    }

    private static int CalculateTimeLimit(List<Questions> questions)
    {
        return 8;
    }

    private static decimal CalculatePassingScore(List<Questions> questions)
    {
        return 8;
    }
}
