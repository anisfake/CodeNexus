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

        if (!path.CreatedByType)
        {
            return Result<UpsertLearningPathMentorReviewResponseDto>.Failure(
                "LEARNING_PATH_NOT_AI_GENERATED",
                "Mentor review is only available for AI-generated learning paths.");
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

        var review = await _context.LearningPathMentorReviews
            .FirstOrDefaultAsync(r => r.PathId == request.PathId && r.MentorId == mentorId, cancellationToken);

        if (review == null)
        {
            review = new LearningPathMentorReview
            {
                ReviewId = Guid.NewGuid(),
                PathId = request.PathId,
                MentorId = mentorId,
                StudentId = studentId,
                Score = request.Score,
                Feedback = normalizedFeedback,
                Suggestions = normalizedSuggestions,
                DecisionStatus = LearningPathMentorReviewDecisionStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            await _context.LearningPathMentorReviews.AddAsync(review, cancellationToken);
        }
        else
        {
            var hasContentChange = review.Score != request.Score
                || !string.Equals(review.Feedback, normalizedFeedback, StringComparison.Ordinal)
                || !string.Equals(review.Suggestions, normalizedSuggestions, StringComparison.Ordinal);

            review.Score = request.Score;
            review.Feedback = normalizedFeedback;
            review.Suggestions = normalizedSuggestions;

            if (hasContentChange)
            {
                review.DecisionStatus = LearningPathMentorReviewDecisionStatus.Pending;
                review.StudentDecisionNote = null;
                review.StudentDecidedAt = null;
            }

            review.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        var stats = await _context.LearningPathMentorReviews
            .AsNoTracking()
            .Where(r => r.PathId == request.PathId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                AverageScore = g.Average(x => (double)x.Score),
                TotalReviews = g.Count()
            })
            .FirstAsync(cancellationToken);

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
                Math.Round(stats.AverageScore, 2),
                stats.TotalReviews));
    }
}
