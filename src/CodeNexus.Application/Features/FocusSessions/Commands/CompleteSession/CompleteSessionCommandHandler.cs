using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.FocusSessions.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.FocusSessions.Commands.CompleteSession;

public class CompleteSessionCommandHandler : IRequestHandler<CompleteSessionCommand, Result<CompleteSessionResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ITaskVerificationService _verificationService;

    public CompleteSessionCommandHandler(
        IApplicationDbContext context,
        ITaskVerificationService verificationService)
    {
        _context = context;
        _verificationService = verificationService;
    }

    public async Task<Result<CompleteSessionResponseDto>> Handle(CompleteSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _context.FocusSessions
            .Include(fs => fs.Task)
            .FirstOrDefaultAsync(fs => fs.SessionId == request.SessionId, cancellationToken);

        if (session == null)
        {
            return Result<CompleteSessionResponseDto>.Failure(
                "SESSION_NOT_FOUND",
                "Session not found");
        }

        if (session.SessionStatus != SessionStatus.Running)
        {
            return Result<CompleteSessionResponseDto>.Failure(
                "SESSION_NOT_RUNNING",
                "Session is not currently running");
        }

        try
        {
            var endTime = DateTime.UtcNow;
            var actualDurationMinutes = (int)(endTime - session.StartTime).TotalMinutes;

            session.EndTime = endTime;
            session.ActualDurationMinutes = actualDurationMinutes;
            session.SubmittedCode = request.SubmittedCode;
            session.SubmittedSummary = request.SubmittedSummary;
            session.SubmittedQuizAnswers = request.SubmittedQuizAnswers;

            if (request.IsEarlyCompletion)
            {
                session.SessionStatus = SessionStatus.CompletedEarly;
            }
            else if (actualDurationMinutes <= session.PlannedDurationMinutes * 0.6)
            {
                session.SessionStatus = SessionStatus.CompletedEarly;
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

            await _context.SaveChangesAsync(cancellationToken);

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
                        ErrorMessage = $"Practice tasks require code submission for {request.SubmissionType.ToString().ToLower()} submission"
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
                        ErrorMessage = $"Theory tasks require summary submission for {request.SubmissionType.ToString().ToLower()} submission"
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
                        ErrorMessage = $"Quiz tasks require quiz answers submission for {request.SubmissionType.ToString().ToLower()} submission"
                    };
                }
                break;
        }

        return new ValidationResult { IsValid = true };
    }

    private static string GetCompletionMessage(Domain.Entities.FocusSession session, SubmissionType submissionType, int actualDurationMinutes, bool taskCompleted)
    {
        var baseMessage = session.SessionStatus == SessionStatus.CompletedEarly
            ? $"Session completed early! You finished {session.PlannedDurationMinutes - actualDurationMinutes} minutes ahead of schedule."
            : "Session completed successfully!";

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