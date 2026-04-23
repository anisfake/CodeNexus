using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPathMentorReviews.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPathMentorReviews.Commands.UpsertLearningPathMentorReview;

public class UpsertLearningPathMentorReviewCommandHandler
    : IRequestHandler<UpsertLearningPathMentorReviewCommand, Result<UpsertLearningPathMentorReviewResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public UpsertLearningPathMentorReviewCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<UpsertLearningPathMentorReviewResponseDto>> Handle(
        UpsertLearningPathMentorReviewCommand request,
        CancellationToken cancellationToken)
    {
        Guid mentorId;
        try
        {
            mentorId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<UpsertLearningPathMentorReviewResponseDto>.Failure("UNAUTHORIZED", "User not authenticated.");
        }

        var mentor = await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == mentorId, cancellationToken);

        if (mentor == null)
        {
            return Result<UpsertLearningPathMentorReviewResponseDto>.Failure("USER_NOT_FOUND", "User not found.");
        }

        if (!string.Equals(mentor.Role?.RoleName, "Mentor", StringComparison.OrdinalIgnoreCase))
        {
            return Result<UpsertLearningPathMentorReviewResponseDto>.Failure("ACCESS_DENIED", "Only mentors can review learning paths.");
        }

        var path = await _context.LearningPaths
            .AsNoTracking()
            .FirstOrDefaultAsync(lp => lp.PathId == request.PathId, cancellationToken);

        if (path == null)
        {
            return Result<UpsertLearningPathMentorReviewResponseDto>.Failure("LEARNING_PATH_NOT_FOUND", "Learning path not found.");
        }

        if (path.UserId == mentorId)
        {
            return Result<UpsertLearningPathMentorReviewResponseDto>.Failure(
                "SELF_REVIEW_NOT_ALLOWED",
                "You cannot review your own learning path.");
        }

        var studentId = path.UserId;

        var normalizedFeedback = request.Feedback?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedFeedback))
        {
            return Result<UpsertLearningPathMentorReviewResponseDto>.Failure(
                "INVALID_FEEDBACK",
                "Feedback is required.");
        }

        var normalizedSuggestions = string.IsNullOrWhiteSpace(request.Suggestions)
            ? null
            : request.Suggestions.Trim();

        var normalizedChangeSummary = string.IsNullOrWhiteSpace(request.ChangeSummary)
            ? normalizedSuggestions
            : request.ChangeSummary.Trim();

        var normalizedChangeReason = string.IsNullOrWhiteSpace(request.ChangeReason)
            ? null
            : request.ChangeReason.Trim();

        var review = await _context.LearningPathMentorReviews
            .FirstOrDefaultAsync(r => r.PathId == request.PathId && r.MentorId == mentorId, cancellationToken);

        if (review == null)
        {
            return Result<UpsertLearningPathMentorReviewResponseDto>.Failure(
                "REVIEW_REQUEST_NOT_FOUND",
                "Student has not requested mentor review for this learning path yet.");
        }

        if (!review.RevisedPathId.HasValue)
        {
            return Result<UpsertLearningPathMentorReviewResponseDto>.Failure(
                "REVISED_PATH_NOT_FOUND",
                "Mentor review workspace path is missing.");
        }

        var revisedPath = await _context.LearningPaths
            .AsNoTracking()
            .FirstOrDefaultAsync(lp => lp.PathId == review.RevisedPathId.Value, cancellationToken);

        if (revisedPath == null || revisedPath.UserId != mentorId)
        {
            return Result<UpsertLearningPathMentorReviewResponseDto>.Failure(
                "REVISED_PATH_NOT_FOUND",
                "Mentor review workspace path is missing.");
        }

        if (review.DecisionStatus == LearningPathMentorReviewDecisionStatus.Accepted)
        {
            return Result<UpsertLearningPathMentorReviewResponseDto>.Failure(
                "REVIEW_ALREADY_ACCEPTED",
                "This review has already been accepted by student.");
        }

        if (IsLimitReached(review.RejectionCount, review.MaxRejections))
        {
            return Result<UpsertLearningPathMentorReviewResponseDto>.Failure(
                "MENTOR_REVIEW_REJECT_LIMIT_REACHED",
                $"Reject limit reached ({FormatLimitForDisplay(review.MaxRejections)}). Student cannot request more revisions.");
        }

        var hasContentChange = review.Score != request.Score
            || !string.Equals(review.Feedback, normalizedFeedback, StringComparison.Ordinal)
            || !string.Equals(review.Suggestions, normalizedSuggestions, StringComparison.Ordinal)
            || !string.Equals(review.ChangeSummary, normalizedChangeSummary, StringComparison.Ordinal)
            || !string.Equals(review.ChangeReason, normalizedChangeReason, StringComparison.Ordinal);

        review.Score = request.Score;
        review.Feedback = normalizedFeedback;
        review.Suggestions = normalizedSuggestions;
        review.ChangeSummary = normalizedChangeSummary;
        review.ChangeReason = normalizedChangeReason;

        if (hasContentChange)
        {
            review.DecisionStatus = LearningPathMentorReviewDecisionStatus.Pending;
            review.StudentDecisionNote = null;
            review.StudentDecidedAt = null;
        }

        review.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        var stats = await _context.LearningPathMentorReviews
            .AsNoTracking()
            .Where(r => r.PathId == request.PathId && r.Score > 0)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                AverageScore = g.Average(x => (double)x.Score),
                TotalReviews = g.Count()
            })
            .FirstOrDefaultAsync(cancellationToken);

        var averageScore = stats?.AverageScore ?? 0d;
        var totalReviews = stats?.TotalReviews ?? 0;

        return Result<UpsertLearningPathMentorReviewResponseDto>.Success(
            new UpsertLearningPathMentorReviewResponseDto(
                review.ReviewId,
                review.PathId,
                review.MentorId,
                review.StudentId,
                review.Score,
                review.Feedback,
                review.Suggestions,
                review.DecisionStatus,
                review.StudentDecisionNote,
                review.StudentDecidedAt,
                review.CreatedAt,
                review.UpdatedAt,
                Math.Round(averageScore, 2),
                totalReviews,
                review.RevisedPathId,
                review.ChangeSummary,
                review.ChangeReason,
                review.RejectionCount,
                review.MaxRejections,
                CanRequestRevision(review.RejectionCount, review.MaxRejections)));
    }

    private static bool IsLimitReached(int used, int limit)
        => limit != -1 && used >= limit;

    private static bool CanRequestRevision(int used, int limit)
        => limit == -1 || used < limit;

    private static string FormatLimitForDisplay(int limit)
        => limit == -1 ? "unlimited" : limit.ToString();
}
