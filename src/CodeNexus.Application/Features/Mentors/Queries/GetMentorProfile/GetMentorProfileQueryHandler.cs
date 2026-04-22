using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Mentors.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Mentors.Queries.GetMentorProfile;

public class GetMentorProfileQueryHandler : IRequestHandler<GetMentorProfileQuery, Result<MentorProfileDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetMentorProfileQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<MentorProfileDto>> Handle(GetMentorProfileQuery request, CancellationToken cancellationToken)
    {
        Guid currentUserId;
        try
        {
            currentUserId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<MentorProfileDto>.Failure("UNAUTHORIZED", "User not authenticated.");
        }

        var mentor = await _context.Users
            .AsNoTracking()
            .Where(u => u.UserId == request.MentorId && u.Role != null && u.Role.RoleName == "Mentor")
            .Select(u => new
            {
                u.UserId,
                u.Username,
                u.Email,
                u.FirstName,
                u.LastName,
                AvatarUrl = u.UserProfile != null ? u.UserProfile.AvatarUrl : null,
                Bio = u.UserProfile != null ? u.UserProfile.Bio : null
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (mentor == null)
        {
            return Result<MentorProfileDto>.Failure("MENTOR_NOT_FOUND", "Mentor not found.");
        }

        var ratingStats = await _context.MentorRatings
            .AsNoTracking()
            .Where(r => r.MentorId == request.MentorId)
            .GroupBy(r => r.MentorId)
            .Select(g => new
            {
                AverageRating = g.Average(x => (double)x.Score),
                TotalReviews = g.Count()
            })
            .FirstOrDefaultAsync(cancellationToken);

        var reviewRows = await _context.MentorRatings
            .AsNoTracking()
            .Where(r => r.MentorId == request.MentorId)
            .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
            .Take(20)
            .Join(
                _context.Users.AsNoTracking(),
                r => r.StudentId,
                u => u.UserId,
                (r, u) => new
                {
                    r.RatingId,
                    r.StudentId,
                    StudentName = u.Username,
                    StudentAvatarUrl = u.UserProfile != null ? u.UserProfile.AvatarUrl : null,
                    r.Score,
                    r.Comment,
                    r.CreatedAt,
                    r.UpdatedAt
                })
            .ToListAsync(cancellationToken);

        var myReviewRow = reviewRows.FirstOrDefault(x => x.StudentId == currentUserId);
        if (myReviewRow == null)
        {
            myReviewRow = await _context.MentorRatings
                .AsNoTracking()
                .Where(r => r.MentorId == request.MentorId && r.StudentId == currentUserId)
                .Join(
                    _context.Users.AsNoTracking(),
                    r => r.StudentId,
                    u => u.UserId,
                    (r, u) => new
                    {
                        r.RatingId,
                        r.StudentId,
                        StudentName = u.Username,
                        StudentAvatarUrl = u.UserProfile != null ? u.UserProfile.AvatarUrl : null,
                        r.Score,
                        r.Comment,
                        r.CreatedAt,
                        r.UpdatedAt
                    })
                .FirstOrDefaultAsync(cancellationToken);
        }

        var subjectRowsFromSubjects = await _context.Subjects
            .AsNoTracking()
            .Where(s => !s.IsDeleted && s.CreatedByUserId == request.MentorId)
            .Select(s => new { s.Name, s.Category })
            .ToListAsync(cancellationToken);

        var subjectRowsFromLearningPaths = await _context.LearningPaths
            .AsNoTracking()
            .Where(lp => lp.UserId == request.MentorId)
            .Join(
                _context.Subjects.AsNoTracking(),
                lp => lp.SubjectId,
                s => s.SubjectId,
                (lp, s) => new { s.Name, s.Category })
            .Distinct()
            .ToListAsync(cancellationToken);

        var allSubjectRows = subjectRowsFromSubjects
            .Concat(subjectRowsFromLearningPaths)
            .ToList();

        var specializations = allSubjectRows
            .Select(x => x.Category.ToString())
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        var specializedSubjects = allSubjectRows
            .Select(x => x.Name)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        var recentReviews = reviewRows
            .Select(x => new MentorReviewDto(
                x.RatingId,
                x.StudentId,
                x.StudentName,
                x.StudentAvatarUrl,
                x.Score,
                x.Comment,
                x.CreatedAt,
                x.UpdatedAt))
            .ToList();

        MentorReviewDto? myReview = myReviewRow == null
            ? null
            : new MentorReviewDto(
                myReviewRow.RatingId,
                myReviewRow.StudentId,
                myReviewRow.StudentName,
                myReviewRow.StudentAvatarUrl,
                myReviewRow.Score,
                myReviewRow.Comment,
                myReviewRow.CreatedAt,
                myReviewRow.UpdatedAt);

        var response = new MentorProfileDto(
            mentor.UserId,
            mentor.Username,
            mentor.Email,
            mentor.FirstName,
            mentor.LastName,
            BuildFullName(mentor.FirstName, mentor.LastName),
            mentor.AvatarUrl,
            mentor.Bio,
            Math.Round(ratingStats?.AverageRating ?? 0d, 2),
            ratingStats?.TotalReviews ?? 0,
            specializations,
            specializedSubjects,
            myReview,
            recentReviews);

        return Result<MentorProfileDto>.Success(response);
    }

    private static string? BuildFullName(string? firstName, string? lastName)
    {
        var fullName = $"{firstName} {lastName}".Trim();
        return string.IsNullOrWhiteSpace(fullName) ? null : fullName;
    }
}
