using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Subjects.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Subjects.Queries.GetSubjects;

public class GetSubjectsQueryHandler : IRequestHandler<GetSubjectsQuery, Result<List<SubjectDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ISubjectCacheService _subjectCacheService;

    public GetSubjectsQueryHandler(IApplicationDbContext context, ISubjectCacheService subjectCacheService)
    {
        _context = context;
        _subjectCacheService = subjectCacheService;
    }

    public async Task<Result<List<SubjectDto>>> Handle(GetSubjectsQuery request, CancellationToken cancellationToken)
    {
        var cachedSubjects = await _subjectCacheService.GetSubjectsAsync(request.Category, cancellationToken);
        if (cachedSubjects != null)
        {
            return Result<List<SubjectDto>>.Success(cachedSubjects);
        }

        var query = _context.Subjects
            .Include(s => s.CreatedByUser)
            .AsNoTracking()
            .Where(s => !s.IsDeleted);

        // Apply category filter if provided
        if (request.Category.HasValue)
        {
            query = query.Where(s => s.Category == request.Category.Value);
        }

        var subjects = await query
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new SubjectDto(
                s.SubjectId,
                s.Name,
                s.Description,
                s.Color,
                s.Icon,
                s.Category,
                s.CreatedByUser.FirstName + " " + s.CreatedByUser.LastName,
                s.CreatedByUserId,
                s.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        await _subjectCacheService.SetSubjectsAsync(request.Category, subjects, TimeSpan.FromMinutes(10), cancellationToken);

        return Result<List<SubjectDto>>.Success(subjects);
    }
}
