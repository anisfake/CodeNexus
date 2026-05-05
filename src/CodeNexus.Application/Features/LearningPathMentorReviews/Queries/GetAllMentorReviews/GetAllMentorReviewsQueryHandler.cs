using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPathMentorReviews.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPathMentorReviews.Queries.GetAllMentorReviews;

public class GetAllMentorReviewsQueryHandler
    : IRequestHandler<GetAllMentorReviewsQuery, Result<List<AdminMentorReviewDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetAllMentorReviewsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<AdminMentorReviewDto>>> Handle(
        GetAllMentorReviewsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.LearningPathMentorReviews
            .AsNoTracking()
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
                r.Student.FirstName + " " + r.Student.LastName,
                r.Student.Email,
                r.MentorId,
                r.Mentor.FirstName + " " + r.Mentor.LastName,
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
