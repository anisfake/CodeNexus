using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Subjects.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Subjects.Queries.GetSubjects;

public class GetSubjectsQueryHandler : IRequestHandler<GetSubjectsQuery, Result<List<SubjectDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetSubjectsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<List<SubjectDto>>> Handle(GetSubjectsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();

        var query = _context.Subjects
            .Include(s => s.CreatedByUser)
            .Include(s => s.SubjectGoals)
                .ThenInclude(sg => sg.Goal)
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
                s.SubjectGoals
                    .Where(sg =>
                        !sg.Goal.IsDeleted &&
                        (sg.Goal.IsSystemDefined || sg.Goal.CreatedByUserId == userId))
                    .OrderBy(sg => sg.Goal.Title)
                    .Select(sg => new SubjectGoalDto(
                        sg.GoalId,
                        sg.Goal.Title,
                        sg.Goal.Description,
                        sg.Goal.IsSystemDefined,
                        sg.Goal.DurationInDays
                    ))
                    .ToList(),
                s.CreatedByUser.FirstName + " " + s.CreatedByUser.LastName,
                s.CreatedByUserId,
                s.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return Result<List<SubjectDto>>.Success(subjects);
    }
}
