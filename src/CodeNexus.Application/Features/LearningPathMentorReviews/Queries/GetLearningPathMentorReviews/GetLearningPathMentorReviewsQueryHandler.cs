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

        var reviewRows = await (from review in _context.LearningPathMentorReviews.AsNoTracking()
                                join mentorUser in _context.Users.AsNoTracking() on review.MentorId equals mentorUser.UserId
                                join mentorUserProfile in _context.UserProfiles.AsNoTracking() on mentorUser.UserId equals mentorUserProfile.UserId
                                join studentSub in _context.StudentMentorSubscriptions.AsNoTracking() on review.StudentId equals studentSub.UserId
                                join pkg in _context.MentorPackages.AsNoTracking() on studentSub.MentorPackageId equals pkg.MentorPackageId
                                where review.PathId == request.PathId && studentSub.IsActive && pkg.IsActive
                                select new
                                {
                                    review.ReviewId,
                                    review.PathId,
                                    review.MentorId,
                                    MentorAvatarUrl = mentorUserProfile.AvatarUrl,
                                    MentorEmail = mentorUser.Email,
                                    MentorName = mentorUser.FirstName + " " + mentorUser.LastName,
                                    review.StudentId,
                                    review.RevisedPathId,
                                    review.StudentRequestNote,
                                    review.ChangeSummary,
                                    review.ChangeReason,
                                    studentSub.ValidationRequestsUsed,
                                    pkg.ValidationRequestLimit,
                                    review.DecisionStatus,
                                    review.StudentDecisionNote,
                                    review.StudentDecidedAt,
                                    review.CreatedAt,
                                    review.UpdatedAt
                                })
                               .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                               .ToListAsync(cancellationToken);

        var reviews = reviewRows.Select(x => new LearningPathMentorReviewDto(
                x.ReviewId,
                x.PathId,
                x.MentorId,
                x.MentorAvatarUrl,
                x.MentorEmail,
                x.MentorName,
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
                x.ValidationRequestsUsed,
                x.ValidationRequestLimit,
                x.ValidationRequestsUsed < x.ValidationRequestLimit))
            .ToList();

        return Result<LearningPathMentorReviewListResponseDto>.Success(
            new LearningPathMentorReviewListResponseDto(
                request.PathId,
                reviews));
    }
}
