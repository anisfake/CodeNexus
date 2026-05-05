using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Mentors.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Mentors.Queries.GetMentors;

public class GetMentorsQueryHandler : IRequestHandler<GetMentorsQuery, Result<PaginationDto<MentorListItemDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetMentorsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PaginationDto<MentorListItemDto>>> Handle(GetMentorsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            _ = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<PaginationDto<MentorListItemDto>>.Failure("UNAUTHORIZED", "User not authenticated.");
        }

        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize <= 0 ? 12 : Math.Min(request.PageSize, 50);
        var searchTerm = request.SearchTerm?.Trim();
        var subjectName = request.SubjectName?.Trim();

        var mentorQuery = _context.Users
            .AsNoTracking()
            .Where(u => u.Role != null && u.Role.RoleName == "Mentor");

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            mentorQuery = mentorQuery.Where(u =>
                EF.Functions.Like(u.Username, $"%{searchTerm}%") ||
                (u.FirstName != null && EF.Functions.Like(u.FirstName, $"%{searchTerm}%")) ||
                (u.LastName != null && EF.Functions.Like(u.LastName, $"%{searchTerm}%")));
        }

        if (request.SubjectCategory.HasValue || !string.IsNullOrWhiteSpace(subjectName))
        {
            var subjectQuery = _context.Subjects
                .AsNoTracking()
                .Where(s => !s.IsDeleted);

            if (request.SubjectCategory.HasValue)
            {
                subjectQuery = subjectQuery.Where(s => s.Category == request.SubjectCategory.Value);
            }

            if (!string.IsNullOrWhiteSpace(subjectName))
            {
                var normalizedSubjectName = subjectName.ToLowerInvariant();
                subjectQuery = subjectQuery.Where(s => s.Name.ToLower().Contains(normalizedSubjectName));
            }

            var mentorIdsFromCreatedSubjects = subjectQuery
                .Select(s => s.CreatedByUserId);

            var mentorIdsFromLearningPathSubjects = _context.LearningPaths
                .AsNoTracking()
                .Join(
                    subjectQuery,
                    lp => lp.SubjectId,
                    s => s.SubjectId,
                    (lp, s) => lp.UserId);

            var filteredMentorIds = mentorIdsFromCreatedSubjects
                .Concat(mentorIdsFromLearningPathSubjects)
                .Distinct();

            mentorQuery = mentorQuery.Where(u => filteredMentorIds.Contains(u.UserId));
        }

        var totalCount = await mentorQuery.CountAsync(cancellationToken);

        var mentorRows = await mentorQuery
            .OrderBy(u => u.Username)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
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
            .ToListAsync(cancellationToken);

        if (mentorRows.Count == 0)
        {
            return Result<PaginationDto<MentorListItemDto>>.Success(new PaginationDto<MentorListItemDto>
            {
                Items = [],
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }

        var mentorIds = mentorRows.Select(x => x.UserId).ToList();

        var ratingStats = await _context.MentorRatings
            .AsNoTracking()
            .Where(r => mentorIds.Contains(r.MentorId))
            .GroupBy(r => r.MentorId)
            .Select(g => new
            {
                MentorId = g.Key,
                AverageRating = g.Average(x => (double)x.Score),
                TotalReviews = g.Count()
            })
            .ToDictionaryAsync(x => x.MentorId, x => new { x.AverageRating, x.TotalReviews }, cancellationToken);

        var specializationPairsFromSubjects = await _context.Subjects
            .AsNoTracking()
            .Where(s => !s.IsDeleted && mentorIds.Contains(s.CreatedByUserId))
            .Select(s => new { MentorId = s.CreatedByUserId, Category = s.Category })
            .Distinct()
            .ToListAsync(cancellationToken);

        var specializationPairsFromLearningPaths = await _context.LearningPaths
            .AsNoTracking()
            .Where(lp => mentorIds.Contains(lp.UserId))
            .Join(
                _context.Subjects.AsNoTracking(),
                lp => lp.SubjectId,
                s => s.SubjectId,
                (lp, s) => new { MentorId = lp.UserId, Category = s.Category })
            .Distinct()
            .ToListAsync(cancellationToken);

        var specializationByMentorId = specializationPairsFromSubjects
            .Concat(specializationPairsFromLearningPaths)
            .GroupBy(x => x.MentorId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.Category.ToString()).Distinct().OrderBy(x => x).ToList());

        var items = mentorRows
            .Select(row =>
            {
                var fullName = BuildFullName(row.FirstName, row.LastName);
                var stats = ratingStats.TryGetValue(row.UserId, out var value)
                    ? value
                    : null;
                var specializations = specializationByMentorId.TryGetValue(row.UserId, out var categories)
                    ? categories
                    : [];

                return new MentorListItemDto(
                    row.UserId,
                    row.Username,
                    row.Email,
                    row.FirstName,
                    row.LastName,
                    fullName,
                    row.AvatarUrl,
                    row.Bio,
                    Math.Round(stats?.AverageRating ?? 0d, 2),
                    stats?.TotalReviews ?? 0,
                    specializations);
            })
            .ToList();

        return Result<PaginationDto<MentorListItemDto>>.Success(new PaginationDto<MentorListItemDto>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    private static string? BuildFullName(string? firstName, string? lastName)
    {
        var fullName = $"{firstName} {lastName}".Trim();
        return string.IsNullOrWhiteSpace(fullName) ? null : fullName;
    }
}
