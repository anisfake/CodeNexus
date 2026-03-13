using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Subjects.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Subjects.Queries.GetSubjects;

public class GetSubjectsQueryHandler : IRequestHandler<GetSubjectsQuery, Result<List<SubjectDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetSubjectsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<SubjectDto>>> Handle(GetSubjectsQuery request, CancellationToken cancellationToken)
    {
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

        return Result<List<SubjectDto>>.Success(subjects);
    }
}
