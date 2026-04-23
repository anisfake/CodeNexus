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
                CanRequestRevision(x.RejectionCount, x.MaxRejections)))
            .ToList();

        return Result<LearningPathMentorReviewListResponseDto>.Success(
            new LearningPathMentorReviewListResponseDto(
                request.PathId,
                reviews));
    }

    private static bool CanRequestRevision(int used, int limit)
        => limit == -1 || used < limit;
}
