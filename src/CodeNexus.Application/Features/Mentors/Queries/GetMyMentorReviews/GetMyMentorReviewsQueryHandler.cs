using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Mentors.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Mentors.Queries.GetMyMentorReviews;

public class GetMyMentorReviewsQueryHandler : IRequestHandler<GetMyMentorReviewsQuery, Result<MentorReviewListResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetMyMentorReviewsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<MentorReviewListResponseDto>> Handle(GetMyMentorReviewsQuery request, CancellationToken cancellationToken)
    {
        Guid mentorId;
        try
        {
            mentorId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<MentorReviewListResponseDto>.Failure("UNAUTHORIZED", "User not authenticated.");
        }

        var currentUser = await _context.Users
            .AsNoTracking()
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.UserId == mentorId, cancellationToken);

        if (currentUser == null)
        {
            return Result<MentorReviewListResponseDto>.Failure("USER_NOT_FOUND", "User not found.");
        }

        if (!string.Equals(currentUser.Role?.RoleName, "Mentor", StringComparison.OrdinalIgnoreCase))
        {
            return Result<MentorReviewListResponseDto>.Failure("ACCESS_DENIED", "Only mentor can view this resource.");
        }

        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize <= 0 ? 20 : Math.Min(request.PageSize, 100);

        var reviewsQuery = _context.MentorRatings
            .AsNoTracking()
            .Where(x => x.MentorId == mentorId);

        var totalCount = await reviewsQuery.CountAsync(cancellationToken);
        var averageRating = totalCount == 0
            ? 0d
            : Math.Round(await reviewsQuery.AverageAsync(x => (double)x.Score, cancellationToken), 2);

        var items = await reviewsQuery
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Join(
                _context.Users.AsNoTracking(),
                review => review.StudentId,
                student => student.UserId,
                (review, student) => new MentorReviewDto(
                    review.RatingId,
                    review.StudentId,
                    student.Username,
                    student.UserProfile != null ? student.UserProfile.AvatarUrl : null,
                    review.Score,
                    review.Comment,
                    review.CreatedAt,
                    review.UpdatedAt))
            .ToListAsync(cancellationToken);

        var pagination = new PaginationDto<MentorReviewDto>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };

        return Result<MentorReviewListResponseDto>.Success(new MentorReviewListResponseDto(
            averageRating,
            totalCount,
            pagination));
    }
}
