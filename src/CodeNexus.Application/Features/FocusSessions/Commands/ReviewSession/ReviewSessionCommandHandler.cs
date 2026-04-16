using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CodeNexus.Application.Features.FocusSessions.Commands.ReviewSession;

public class ReviewSessionCommandHandler : IRequestHandler<ReviewSessionCommand, Result<ReviewSessionResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ITaskVerificationService _verificationService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPlanUsageLimitService _planUsageLimitService;
    private readonly ILogger<ReviewSessionCommandHandler> _logger;

    public ReviewSessionCommandHandler(
        IApplicationDbContext context,
        ITaskVerificationService verificationService,
        ICurrentUserService currentUserService,
        IPlanUsageLimitService planUsageLimitService,
        ILogger<ReviewSessionCommandHandler> logger)
    {
        _context = context;
        _verificationService = verificationService;
        _currentUserService = currentUserService;
        _planUsageLimitService = planUsageLimitService;
        _logger = logger;
    }

    public async Task<Result<ReviewSessionResponseDto>> Handle(ReviewSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _context.FocusSessions
            .Include(s => s.Task)
            .FirstOrDefaultAsync(s => s.SessionId == request.SessionId, cancellationToken);

        if (session == null)
        {
            return Result<ReviewSessionResponseDto>.Failure("SESSION_NOT_FOUND", "Session not found");
        }

        if (session.SessionStatus != SessionStatus.Running)
        {
            return Result<ReviewSessionResponseDto>.Failure("SESSION_NOT_RUNNING", "Session is not running");
        }

        if (session.Task.TaskType == TaskType.Practice && string.IsNullOrWhiteSpace(request.SubmittedCode))
        {
            return Result<ReviewSessionResponseDto>.Failure("MISSING_CODE_SUBMISSION", "Code submission is required for coding tasks");
        }

        if (session.Task.TaskType == TaskType.Theory && string.IsNullOrWhiteSpace(request.SubmittedSummary))
        {
            return Result<ReviewSessionResponseDto>.Failure("MISSING_SUMMARY_SUBMISSION", "Summary submission is required for summary tasks");
        }

        if (session.Task.TaskType == TaskType.Quizz && string.IsNullOrWhiteSpace(request.SubmittedQuizAnswers))
        {
            return Result<ReviewSessionResponseDto>.Failure("MISSING_QUIZ_ANSWERS", "Quiz answers submission is required for quiz tasks");
        }

        var userId = _currentUserService.GetUserId();
        var limitCheck = await _planUsageLimitService.CheckFocusSessionReviewAllowedAsync(userId, cancellationToken);
        if (!limitCheck.IsSuccess)
        {
            return Result<ReviewSessionResponseDto>.Failure(
                limitCheck.ErrorCode!,
                limitCheck.ErrorMessage!);
        }

        session.LastActivityAt = DateTime.UtcNow;
        session.SubmittedCode = request.SubmittedCode ?? session.SubmittedCode;
        session.SubmittedSummary = request.SubmittedSummary ?? session.SubmittedSummary;
        session.SubmittedQuizAnswers = request.SubmittedQuizAnswers ?? session.SubmittedQuizAnswers;

        VerificationResult verificationResult;
        try
        {
            if (session.Task.TaskType == TaskType.Practice)
            {
                verificationResult = await _verificationService.VerifyCodeSubmissionAsync(
                    session.Task.Title,
                    session.Task.Description ?? "",
                    request.SubmittedCode!,
                    session.Task.VerificationPrompt);
            }
            else if (session.Task.TaskType == TaskType.Theory)
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
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "AI review failed for session {SessionId}, task {TaskId}, user {UserId}",
                session.SessionId,
                session.TaskId,
                userId);

            await _context.SaveChangesAsync(cancellationToken);
            return Result<ReviewSessionResponseDto>.Failure(
                "AI_REVIEW_FAILED",
                "AI review is temporarily unavailable. Please try again.");
        }

        await _planUsageLimitService.RecordFocusSessionReviewUsageAsync(userId, cancellationToken);

        var response = new ReviewSessionResponseDto(
            request.SessionId,
            DateTime.UtcNow,
            verificationResult.Feedback,
            verificationResult.Score,
            "Code reviewed successfully"
        );

        await _context.SaveChangesAsync(cancellationToken);

        return Result<ReviewSessionResponseDto>.Success(response);
    }
}

public record ReviewSessionResponseDto(
    Guid SessionId,
    DateTime ReviewTime,
    string? AIFeedback,
    int? VerificationScore,
    string Message
);
