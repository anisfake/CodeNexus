using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.TaskReviews.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.TaskReviews.Queries.GetTaskReview;

public class GetTaskReviewQueryHandler : IRequestHandler<GetTaskReviewQuery, Result<TaskReviewDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetTaskReviewQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<TaskReviewDto>> Handle(GetTaskReviewQuery request, CancellationToken cancellationToken)
    {
        Guid currentUserId;
        try
        {
            currentUserId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<TaskReviewDto>.Failure("UNAUTHORIZED", "User not authenticated.");
        }

        var review = await _context.TaskReviews
            .AsNoTracking()
            .Include(r => r.Task)
            .Include(r => r.Student)
                .ThenInclude(u => u.UserProfile)
            .Include(r => r.Mentor)
                .ThenInclude(u => u.UserProfile)
            .Include(r => r.Session)
            .FirstOrDefaultAsync(r => r.ReviewId == request.ReviewId, cancellationToken);

        if (review == null)
        {
            return Result<TaskReviewDto>.Failure("REVIEW_NOT_FOUND", "Task review not found.");
        }

        if (review.StudentId != currentUserId && review.MentorId != currentUserId)
        {
            return Result<TaskReviewDto>.Failure("UNAUTHORIZED", "Access denied.");
        }

        var dto = new TaskReviewDto(
            review.ReviewId,
            review.SessionId,
            review.TaskId,
            review.Task.Title,
            review.StudentId,
            review.Student.Username,
            review.MentorId,
            review.Mentor.Username,
            review.Score,
            review.Feedback,
            review.Suggestions,
            review.StudentRequestNote,
            review.Status.ToString(),
            review.RequestedAt,
            review.ReviewedAt,
            review.Session.SubmittedCode,
            review.Session.SubmittedSummary,
            review.Session.SubmittedQuizAnswers,
            review.Session.AIFeedback,
            review.Session.VerificationScore,
            review.Session.IsVerified
        );

        return Result<TaskReviewDto>.Success(dto);
    }
}
