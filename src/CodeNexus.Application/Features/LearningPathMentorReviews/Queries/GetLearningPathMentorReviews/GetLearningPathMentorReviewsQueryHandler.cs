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
        if (!isStudentOwner && !isMentor)
        {
            return Result<LearningPathMentorReviewListResponseDto>.Failure("ACCESS_DENIED", "Access denied.");
        }

        var reviews = await _context.LearningPathMentorReviews
            .AsNoTracking()
            .Where(r => r.PathId == request.PathId)
            .Join(
                _context.Users.AsNoTracking(),
                r => r.MentorId,
                u => u.UserId,
                (r, u) => new LearningPathMentorReviewDto(
                    r.ReviewId,
                    r.PathId,
                    r.MentorId,
                    u.Username,
                    r.StudentId,
                    r.Score,
                    r.Feedback,
                    r.Suggestions,
                    r.DecisionStatus,
                    r.StudentDecisionNote,
                    r.StudentDecidedAt,
                    r.CreatedAt,
                    r.UpdatedAt))
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .ToListAsync(cancellationToken);

        var totalReviews = reviews.Count;
        var avgScore = totalReviews == 0
            ? 0d
            : Math.Round(reviews.Average(x => x.Score), 2);

        return Result<LearningPathMentorReviewListResponseDto>.Success(
            new LearningPathMentorReviewListResponseDto(
                request.PathId,
                avgScore,
                totalReviews,
                reviews));
    }
}
