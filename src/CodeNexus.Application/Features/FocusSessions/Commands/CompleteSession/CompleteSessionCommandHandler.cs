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

            if (request.IsEarlyCompletion)
            {
                session.SessionStatus = SessionStatus.CompletedEarly;
            }
            else if (actualDurationMinutes <= session.PlannedDurationMinutes * 0.6) // If completed in 60% or less of planned time
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
            bool hasValidSubmission = false;

            switch (taskType)
            {
                case TaskType.Practice:
                    hasValidSubmission = !string.IsNullOrEmpty(request.SubmittedCode);
                    if (!hasValidSubmission)
                    {
                        return Result<CompleteSessionResponseDto>.Failure(
                            "MISSING_CODE_SUBMISSION",
                            "Practice tasks require code submission");
                    }
                    break;

                case TaskType.Theory:
                    hasValidSubmission = !string.IsNullOrEmpty(request.SubmittedSummary);
                    if (!hasValidSubmission)
                    {
                        return Result<CompleteSessionResponseDto>.Failure(
                            "MISSING_SUMMARY_SUBMISSION",
                            "Theory tasks require summary submission");
                    }
                    break;

                case TaskType.Quizz:
                    hasValidSubmission = true;
                    break;
            }

            if (hasValidSubmission && taskType != TaskType.Quizz)
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
                    else
                    {
                        verificationResult = await _verificationService.VerifySummarySubmissionAsync(
                            session.Task.Title,
                            session.Task.Description ?? "",
                            request.SubmittedSummary!,
                            session.Task.VerificationPrompt);
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

            var message = session.SessionStatus == SessionStatus.CompletedEarly
                ? $"Session completed early! You finished {session.PlannedDurationMinutes - actualDurationMinutes} minutes ahead of schedule."
                : "Session completed successfully!";

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
}