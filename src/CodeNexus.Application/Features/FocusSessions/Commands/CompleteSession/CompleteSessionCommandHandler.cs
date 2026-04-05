using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Helpers;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.FocusSessions.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.FocusSessions.Commands.CompleteSession;

public class CompleteSessionCommandHandler : IRequestHandler<CompleteSessionCommand, Result<CompleteSessionResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ITaskVerificationService _verificationService;
    private readonly IAchievementService _achievementService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPlanUsageLimitService _planUsageLimitService;

    public CompleteSessionCommandHandler(
        IApplicationDbContext context,
        ITaskVerificationService verificationService,
        IAchievementService achievementService,
        ICurrentUserService currentUserService,
        IPlanUsageLimitService planUsageLimitService)
    {
        _context = context;
        _verificationService = verificationService;
        _achievementService = achievementService;
        _currentUserService = currentUserService;
        _planUsageLimitService = planUsageLimitService;
    }

    public async Task<Result<CompleteSessionResponseDto>> Handle(CompleteSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _context.FocusSessions
            .Include(fs => fs.Task)
            .ThenInclude(lp => lp.LearningPath)
            .ThenInclude(lp => lp.User)
            .FirstOrDefaultAsync(fs => fs.SessionId == request.SessionId, cancellationToken);

        if (session == null)
        {
            return Result<CompleteSessionResponseDto>.Failure(
                "SESSION_NOT_FOUND",
                "Session not found");
        }

        if (session.SessionStatus != SessionStatus.Running &&
            session.SessionStatus != SessionStatus.Paused)
        {
            return Result<CompleteSessionResponseDto>.Failure(
                "SESSION_NOT_RUNNING",
                "Session is not running");
        }

        try
        {
            var endTime = DateTime.UtcNow;
            var actualDurationMinutes = CalculateElapsedMinutes(session, endTime, finalizePause: true);

            session.EndTime = endTime;
            session.ActualDurationMinutes = actualDurationMinutes;
            session.SubmittedCode = request.SubmittedCode;
            session.SubmittedSummary = request.SubmittedSummary;
            session.SubmittedQuizAnswers = request.SubmittedQuizAnswers;

            if (request.IsEarlyCompletion)
            {
                session.SessionStatus = SessionStatus.CompletedEarly;
            }
            else if (session.PlannedDurationMinutes > 0 &&
                     actualDurationMinutes <= session.PlannedDurationMinutes * 0.6)
            {
                session.SessionStatus = SessionStatus.CompletedEarly;
            }
            else if (session.PlannedDurationMinutes > 0 &&
                     actualDurationMinutes > session.PlannedDurationMinutes)
            {
                session.SessionStatus = SessionStatus.CompletedLate;
            }
            else
            {
                session.SessionStatus = SessionStatus.CompletedOnTime;
            }

            bool taskCompleted = false;
            string? aiFeedback = null;
            int? verificationScore = null;

            var taskType = session.Task.TaskType;

            var validationResult = ValidateSubmission(request, taskType);
            if (!validationResult.IsValid)
            {
                return Result<CompleteSessionResponseDto>.Failure(
                    validationResult.ErrorCode!,
                    validationResult.ErrorMessage!);
            }

            if (request.SubmissionType == SubmissionType.Final)
            {
                var userIdForLimit = _currentUserService.GetUserId();
                var focusReviewLimitCheck = await _planUsageLimitService.CheckFocusSessionReviewAllowedAsync(userIdForLimit, cancellationToken);
                if (!focusReviewLimitCheck.IsSuccess)
                {
                    return Result<CompleteSessionResponseDto>.Failure(
                        focusReviewLimitCheck.ErrorCode!,
                        focusReviewLimitCheck.ErrorMessage!);
                }

                try
                {
                    VerificationResult verificationResult;

                    if (taskType == TaskType.Practice)
                    {
                        verificationResult = await _verificationService.VerifyCodeSubmissionAsync(
                            session.Task.Title,
                            session.Task.Description ?? "",
                            request.SubmittedCode!,
                            session.Task.VerificationPrompt);
                    }
                    else if (taskType == TaskType.Theory)
                    {
                        verificationResult = await _verificationService.VerifySummarySubmissionAsync(
                            session.Task.Title,
                            session.Task.Description ?? "",
                            request.SubmittedSummary!,
                            session.Task.VerificationPrompt);
                    }
                    else
                    {
                        verificationResult = await _verificationService.VerifyQuizSubmissionAsync(
                            session.Task.Title,
                            session.Task.Description ?? "",
                            session.Task.QuizQuestionsJson!,
                            request.SubmittedQuizAnswers!);
                    }

                    session.AIFeedback = verificationResult.Feedback;
                    session.VerificationScore = verificationResult.Score;
                    session.IsVerified = verificationResult.IsPass;

                    aiFeedback = verificationResult.Feedback;
                    verificationScore = verificationResult.Score;
                    await _planUsageLimitService.RecordFocusSessionReviewUsageAsync(userIdForLimit, cancellationToken);

                    if (verificationResult.IsPass)
                    {
                        session.Task.Status = TaskStatus_.Completed;
                        session.Task.CompletedAt = DateTime.UtcNow;
                        taskCompleted = true;
                    }
                }
                catch (Exception ex)
                {
                    session.AIFeedback = $"Verification failed: {ex.Message}";
                    aiFeedback = session.AIFeedback;
                }
            }

            await TryCreateDailyCheckinAsync(session, request.SubmissionType, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);

            var hasChapterCompletionChanges = await ChapterCompletionSyncHelper.SyncAsync(
                _context,
                session.Task.ChapterId,
                session.Task.LearningPath.UserId,
                cancellationToken);

            var hasGoalProgressChanges = await UserGoalProgressSyncHelper.SyncForLearningPathAsync(
                _context,
                session.Task.PathId,
                session.Task.LearningPath.UserId,
                cancellationToken);

            if (hasGoalProgressChanges || hasChapterCompletionChanges)
            {
                await _context.SaveChangesAsync(cancellationToken);
            }

            var userId = session.Task.LearningPath.UserId;
            await _achievementService.TryUnlockAsync(userId, "focused_learner");
            if (actualDurationMinutes >= 90) await _achievementService.TryUnlockAsync(userId, "deep_focus");
            if (DateTime.UtcNow.Hour >= 5 && DateTime.UtcNow.Hour < 8) await _achievementService.TryUnlockAsync(userId, "early_bird");
            if (DateTime.UtcNow.Hour >= 22 || DateTime.UtcNow.Hour < 2) await _achievementService.TryUnlockAsync(userId, "night_owl");

            var minutesEarly = session.PlannedDurationMinutes - actualDurationMinutes;
            if (minutesEarly >= 30) await _achievementService.TryUnlockAsync(userId, "speed_demon");

            if (endTime.DayOfWeek == DayOfWeek.Saturday || endTime.DayOfWeek == DayOfWeek.Sunday)
                await _achievementService.TryUnlockAsync(userId, "weekend_warrior");

            if (verificationScore == 100) await _achievementService.TryUnlockAsync(userId, "perfectionist");

            var message = GetCompletionMessage(session, request.SubmissionType, actualDurationMinutes, taskCompleted);

            var responseDto = new CompleteSessionResponseDto(
                session.SessionId,
                endTime,
                actualDurationMinutes,
                session.SessionStatus.ToString(),
                message,
                taskCompleted,
                aiFeedback,
                verificationScore
            );

            return Result<CompleteSessionResponseDto>.Success(responseDto);
        }
        catch (Exception ex)
        {
            return Result<CompleteSessionResponseDto>.Failure(
                "COMPLETE_SESSION_FAILED",
                $"An error occurred while completing the session: {ex.Message}");
        }
    }

    private static int CalculateElapsedMinutes(FocusSession session, DateTime now, bool finalizePause)
    {
        var pausedMinutes = session.TotalPausedMinutes;
        if (session.PausedAt.HasValue)
        {
            var extra = (int)(now - session.PausedAt.Value).TotalMinutes;
            if (extra > 0)
            {
                pausedMinutes += extra;
                if (finalizePause)
                {
                    session.TotalPausedMinutes = pausedMinutes;
                    session.PausedAt = null;
                }
            }
        }

        var elapsed = (int)(now - session.StartTime).TotalMinutes - pausedMinutes;
        return Math.Max(0, elapsed);
    }

    private async Task TryCreateDailyCheckinAsync(FocusSession session, SubmissionType submissionType, CancellationToken cancellationToken)
    {
        if (submissionType != SubmissionType.Final)
        {
            return;
        }

        if (!IsCompletedStatus(session.SessionStatus))
        {
            return;
        }

        var today = VietnamDateTimeHelper.GetTodayDate();
        var userId = session.Task.LearningPath.UserId;

        var (mood, productivity) = DailyCheckinEvaluationHelper.Evaluate(session);
        var existing = await _context.DailyCheckins
            .FirstOrDefaultAsync(x => x.UserId == userId && x.CheckinDate == today, cancellationToken);

        if (existing != null)
        {
            var merged = DailyCheckinEvaluationHelper.Merge(existing.Productivity, productivity);
            existing.Mood = merged.Mood;
            existing.Productivity = merged.Productivity;
            return;
        }

        _context.DailyCheckins.Add(new DailyCheckins
        {
            CheckinId = NewId.NextGuid(),
            UserId = userId,
            CheckinDate = today,
            Mood = mood,
            Productivity = productivity,
            CreatedAt = DateTime.UtcNow
        });
    }

    private static bool IsCompletedStatus(SessionStatus sessionStatus)
    {
        return sessionStatus == SessionStatus.CompletedEarly
            || sessionStatus == SessionStatus.CompletedOnTime
            || sessionStatus == SessionStatus.CompletedLate;
    }

    private static ValidationResult ValidateSubmission(CompleteSessionCommand request, TaskType taskType)
    {
        if (request.SubmissionType == SubmissionType.Progress)
        {
            return new ValidationResult { IsValid = true };
        }

        switch (taskType)
        {
            case TaskType.Practice:
                if (string.IsNullOrEmpty(request.SubmittedCode))
                {
                    return new ValidationResult
                    {
                        IsValid = false,
                        ErrorCode = "MISSING_CODE_SUBMISSION",
                        ErrorMessage = "Code submission is required for coding tasks"
                    };
                }
                break;

            case TaskType.Theory:
                if (string.IsNullOrEmpty(request.SubmittedSummary))
                {
                    return new ValidationResult
                    {
                        IsValid = false,
                        ErrorCode = "MISSING_SUMMARY_SUBMISSION",
                        ErrorMessage = "Summary submission is required for summary tasks"
                    };
                }
                break;

            case TaskType.Quizz:
                if (string.IsNullOrEmpty(request.SubmittedQuizAnswers))
                {
                    return new ValidationResult
                    {
                        IsValid = false,
                        ErrorCode = "MISSING_QUIZ_ANSWERS",
                        ErrorMessage = "Quiz answers submission is required for quiz tasks"
                    };
                }
                break;
        }

        return new ValidationResult { IsValid = true };
    }

    private static string GetCompletionMessage(Domain.Entities.FocusSession session, SubmissionType submissionType, int actualDurationMinutes, bool taskCompleted)
    {
        var baseMessage = session.SessionStatus switch
        {
            SessionStatus.CompletedEarly => $"Session completed early! You finished {session.PlannedDurationMinutes - actualDurationMinutes} minutes ahead of schedule.",
            SessionStatus.CompletedLate => $"Session completed late. You exceeded the planned duration by {actualDurationMinutes - session.PlannedDurationMinutes} minutes.",
            _ => "Session completed successfully!"
        };

        return submissionType switch
        {
            SubmissionType.Progress => $"{baseMessage} Progress saved.",
            SubmissionType.Final when taskCompleted => $"{baseMessage} Task completed!",
            SubmissionType.Final when !taskCompleted => $"{baseMessage} Final submission received, but task verification failed.",
            _ => baseMessage
        };
    }

    private class ValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
