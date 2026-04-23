using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPathMentorReviews.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPathMentorReviews.Queries.GetLearningPathMentorReviews;

public class GetLearningPathMentorReviewsQueryHandler
    : IRequestHandler<GetLearningPathMentorReviewsQuery, Result<LearningPathMentorReviewListResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetLearningPathMentorReviewsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<LearningPathMentorReviewListResponseDto>> Handle(
        GetLearningPathMentorReviewsQuery request,
        CancellationToken cancellationToken)
    {
        Guid currentUserId;
        try
        {
            currentUserId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<LearningPathMentorReviewListResponseDto>.Failure("UNAUTHORIZED", "User not authenticated.");
        }

        var currentUser = await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == currentUserId, cancellationToken);

        if (currentUser == null)
        {
            return Result<LearningPathMentorReviewListResponseDto>.Failure("USER_NOT_FOUND", "User not found.");
        }

        var path = await _context.LearningPaths
            .AsNoTracking()
            .FirstOrDefaultAsync(lp => lp.PathId == request.PathId, cancellationToken);

        if (path == null)
        {
            return Result<LearningPathMentorReviewListResponseDto>.Failure("LEARNING_PATH_NOT_FOUND", "Learning path not found.");
        }

        var roleName = currentUser.Role?.RoleName ?? string.Empty;
        var isStudentOwner = string.Equals(roleName, "Student", StringComparison.OrdinalIgnoreCase)
                             && path.UserId == currentUserId;

        var isMentor = string.Equals(roleName, "Mentor", StringComparison.OrdinalIgnoreCase);
        var isMentorReviewer = false;
        if (isMentor)
        {
            isMentorReviewer = await _context.LearningPathMentorReviews
                .AsNoTracking()
                .AnyAsync(
                    r => r.PathId == request.PathId && r.MentorId == currentUserId,
                    cancellationToken);
        }

        if (!isStudentOwner && !isMentorReviewer)
        {
            return Result<LearningPathMentorReviewListResponseDto>.Failure("ACCESS_DENIED", "Access denied.");
        }

        var reviewRows = await _context.LearningPathMentorReviews
            .AsNoTracking()
            .Where(r => r.PathId == request.PathId)
            .Join(
                _context.Users.AsNoTracking(),
                r => r.MentorId,
                u => u.UserId,
                (r, u) => new
                {
                    r.ReviewId,
                    r.PathId,
                    r.MentorId,
                    MentorUsername = u.Username,
                    r.StudentId,
                    r.Score,
                    r.Feedback,
                    r.Suggestions,
                    r.RevisedPathId,
                    r.StudentRequestNote,
                    r.ChangeSummary,
                    r.ChangeReason,
                    r.RejectionCount,
                    r.MaxRejections,
                    r.DecisionStatus,
                    r.StudentDecisionNote,
                    r.StudentDecidedAt,
                    r.CreatedAt,
                    r.UpdatedAt
                })
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .ToListAsync(cancellationToken);

        var reviews = reviewRows
            .Select(x => new LearningPathMentorReviewDto(
                x.ReviewId,
                x.PathId,
                x.MentorId,
                x.MentorUsername,
                x.StudentId,
                x.Score,
                x.Feedback,
                x.Suggestions,
                x.DecisionStatus,
                x.StudentDecisionNote,
                x.StudentDecidedAt,
                x.CreatedAt,
                x.UpdatedAt,
                x.RevisedPathId,
                x.StudentRequestNote,
                x.ChangeSummary,
                x.ChangeReason,
                x.RejectionCount,
                x.MaxRejections,
                x.RejectionCount < x.MaxRejections))
            .ToList();

        var submittedReviews = reviews.Where(r => r.Score > 0).ToList();
        var totalReviews = submittedReviews.Count;
        var avgScore = totalReviews == 0
            ? 0d
            : Math.Round(submittedReviews.Average(x => x.Score), 2);

        return Result<LearningPathMentorReviewListResponseDto>.Success(
            new LearningPathMentorReviewListResponseDto(
                request.PathId,
                avgScore,
                totalReviews,
                reviews));
    }
}
