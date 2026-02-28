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
        var subjects = await _context.Subjects
            .Include(s => s.CreatedByUser)
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new SubjectDto(
                s.SubjectId,
                s.Name,
                s.Description,
                s.Color,
                s.Icon,
                s.CreatedByUser.FirstName + " " + s.CreatedByUser.LastName,
                s.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return Result<List<SubjectDto>>.Success(subjects);
    }
}
