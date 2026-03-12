using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.FocusSessions.Commands.ReviewSession;

public class ReviewSessionCommandHandler : IRequestHandler<ReviewSessionCommand, Result<ReviewSessionResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ITaskVerificationService _verificationService;

    public ReviewSessionCommandHandler(IApplicationDbContext context, ITaskVerificationService verificationService)
    {
        _context = context;
        _verificationService = verificationService;
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
            return Result<ReviewSessionResponseDto>.Failure("SESSION_NOT_RUNNING", "Session is not currently running");
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

        string? aiFeedback = null;
        int? verificationScore = null;

        try
        {
            VerificationResult verificationResult;

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

            aiFeedback = verificationResult.Feedback;
            verificationScore = verificationResult.Score;
        }
        catch (Exception)
        {
            aiFeedback = "Unable to generate feedback at this time. Please try again later.";
        }

        var response = new ReviewSessionResponseDto(
            request.SessionId,
            DateTime.UtcNow,
            aiFeedback,
            verificationScore,
            "Code reviewed successfully"
        );

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