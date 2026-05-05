using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPathMentorReviews.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPathMentorReviews.Queries.GetMyStudentLearningPathReviews;

public class GetMyStudentLearningPathReviewsQueryHandler
    : IRequestHandler<GetMyStudentLearningPathReviewsQuery, Result<List<AdminMentorReviewDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetMyStudentLearningPathReviewsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<List<AdminMentorReviewDto>>> Handle(
        GetMyStudentLearningPathReviewsQuery request,
        CancellationToken cancellationToken)
    {
        Guid studentId;
        try
        {
            studentId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<List<AdminMentorReviewDto>>.Failure("UNAUTHORIZED", "User not authenticated.");
        }

        var query = _context.LearningPathMentorReviews
            .AsNoTracking()
            .Where(r => r.StudentId == studentId)
            .Include(r => r.Student)
            .Include(r => r.Mentor)
            .Include(r => r.LearningPath)
            .AsQueryable();

        if (request.Status.HasValue)
            query = query.Where(r => r.DecisionStatus == request.Status.Value);

        var reviews = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(r => new AdminMentorReviewDto(
                r.ReviewId,
                r.PathId,
                r.LearningPath.Title,
                r.StudentId,
                r.Student.FirstName ?? r.Student.Username,
                r.Student.Email,
                r.MentorId,
                r.Mentor.FirstName ?? r.Mentor.Username,
                r.Mentor.Email,
                r.DecisionStatus,
                r.StudentRequestNote,
                r.ChangeSummary,
                r.ChangeReason,
                r.StudentDecisionNote,
                r.StudentDecidedAt,
                r.MentorRespondedAt,
                r.CreatedAt,
                r.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Result<List<AdminMentorReviewDto>>.Success(reviews);
    }
}
