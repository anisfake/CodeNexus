using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPathMentorReviews.DTOs;
using CodeNexus.Application.Features.LearningPathShares.Services;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPathMentorReviews.Commands.RespondLearningPathMentorReview;

public class RespondLearningPathMentorReviewCommandHandler
    : IRequestHandler<RespondLearningPathMentorReviewCommand, Result<RespondLearningPathMentorReviewResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILearningPathSharePathSyncService _pathSyncService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RespondLearningPathMentorReviewCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILearningPathSharePathSyncService pathSyncService,
        IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _currentUserService = currentUserService;
        _pathSyncService = pathSyncService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<RespondLearningPathMentorReviewResponseDto>> Handle(
        RespondLearningPathMentorReviewCommand request,
        CancellationToken cancellationToken)
    {
        Guid studentId;
        try
        {
            studentId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<RespondLearningPathMentorReviewResponseDto>.Failure("UNAUTHORIZED", "User not authenticated.");
        }

        var student = await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == studentId, cancellationToken);

        if (student == null)
        {
            return Result<RespondLearningPathMentorReviewResponseDto>.Failure("USER_NOT_FOUND", "User not found.");
        }

        if (!string.Equals(student.Role?.RoleName, "Student", StringComparison.OrdinalIgnoreCase))
        {
            return Result<RespondLearningPathMentorReviewResponseDto>.Failure("ACCESS_DENIED", "Only students can respond to mentor reviews.");
        }

        var path = await _context.LearningPaths
            .AsNoTracking()
            .FirstOrDefaultAsync(lp => lp.PathId == request.PathId, cancellationToken);

        if (path == null)
        {
            return Result<RespondLearningPathMentorReviewResponseDto>.Failure("LEARNING_PATH_NOT_FOUND", "Learning path not found.");
        }

        if (path.UserId != studentId)
        {
            return Result<RespondLearningPathMentorReviewResponseDto>.Failure("ACCESS_DENIED", "You can only respond to reviews on your own learning path.");
        }

        var review = await _context.LearningPathMentorReviews
            .FirstOrDefaultAsync(r => r.ReviewId == request.ReviewId && r.PathId == request.PathId, cancellationToken);

        if (review == null)
        {
            return Result<RespondLearningPathMentorReviewResponseDto>.Failure("REVIEW_NOT_FOUND", "Mentor review not found.");
        }

        if (request.DecisionStatus == LearningPathMentorReviewDecisionStatus.Rejected
            && IsLimitReached(review.RejectionCount, review.MaxRejections))
        {
            return Result<RespondLearningPathMentorReviewResponseDto>.Failure(
                "MENTOR_REVIEW_REJECT_LIMIT_REACHED",
                $"You have reached the maximum reject attempts ({FormatLimitForDisplay(review.MaxRejections)}) for this mentor review.");
        }

        if (request.DecisionStatus == LearningPathMentorReviewDecisionStatus.Accepted)
        {
            if (!review.RevisedPathId.HasValue)
            {
                return Result<RespondLearningPathMentorReviewResponseDto>.Failure(
                    "REVISED_PATH_NOT_FOUND",
                    "Mentor revised learning path not found.");
            }

            var sourcePath = await _context.LearningPaths
                .AsNoTracking()
                .Include(lp => lp.LearningPathGoals)
                .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                    .ThenInclude(c => c.Lessons.Where(l => !l.IsDeleted))
                        .ThenInclude(l => l.Quizzes.Where(q => !q.IsDeleted))
                .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                    .ThenInclude(c => c.Tasks.Where(t => !t.IsDeleted))
                .FirstOrDefaultAsync(lp => lp.PathId == review.RevisedPathId.Value, cancellationToken);

            if (sourcePath == null || sourcePath.UserId != review.MentorId)
            {
                return Result<RespondLearningPathMentorReviewResponseDto>.Failure(
                    "REVISED_PATH_NOT_FOUND",
                    "Mentor revised learning path not found.");
            }

            var targetPath = await _context.LearningPaths
                .Include(lp => lp.LearningPathGoals)
                .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                    .ThenInclude(c => c.Lessons.Where(l => !l.IsDeleted))
                        .ThenInclude(l => l.Quizzes.Where(q => !q.IsDeleted))
                .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                    .ThenInclude(c => c.Tasks.Where(t => !t.IsDeleted))
                .FirstOrDefaultAsync(lp => lp.PathId == path.PathId && lp.UserId == studentId, cancellationToken);

            if (targetPath == null)
            {
                return Result<RespondLearningPathMentorReviewResponseDto>.Failure(
                    "LEARNING_PATH_NOT_FOUND",
                    "Learning path not found.");
            }

            var nowLocal = _dateTimeProvider.UtcNow.AddHours(7);
            await _pathSyncService.RebuildCurrentPathFromSourceAsync(
                targetPath,
                sourcePath,
                studentId,
                nowLocal,
                cancellationToken);
        }

        review.DecisionStatus = request.DecisionStatus;
        review.StudentDecisionNote = string.IsNullOrWhiteSpace(request.StudentDecisionNote)
            ? null
            : request.StudentDecisionNote.Trim();
        review.StudentDecidedAt = DateTime.UtcNow;
        if (request.DecisionStatus == LearningPathMentorReviewDecisionStatus.Rejected)
        {
            review.RejectionCount++;
        }
        review.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return Result<RespondLearningPathMentorReviewResponseDto>.Success(
            new RespondLearningPathMentorReviewResponseDto(
                review.ReviewId,
                review.PathId,
                review.DecisionStatus,
                review.StudentDecisionNote,
                review.StudentDecidedAt,
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
