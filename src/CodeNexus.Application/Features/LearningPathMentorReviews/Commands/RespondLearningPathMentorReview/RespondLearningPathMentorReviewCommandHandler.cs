using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPathMentorReviews.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPathMentorReviews.Commands.RespondLearningPathMentorReview;

public class RespondLearningPathMentorReviewCommandHandler
    : IRequestHandler<RespondLearningPathMentorReviewCommand, Result<RespondLearningPathMentorReviewResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public RespondLearningPathMentorReviewCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
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

        review.DecisionStatus = request.DecisionStatus;
        review.StudentDecisionNote = string.IsNullOrWhiteSpace(request.StudentDecisionNote)
            ? null
            : request.StudentDecisionNote.Trim();
        review.StudentDecidedAt = DateTime.UtcNow;
        review.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return Result<RespondLearningPathMentorReviewResponseDto>.Success(
            new RespondLearningPathMentorReviewResponseDto(
                review.ReviewId,
                review.PathId,
                review.DecisionStatus,
                review.StudentDecisionNote,
                review.StudentDecidedAt));
    }
}
