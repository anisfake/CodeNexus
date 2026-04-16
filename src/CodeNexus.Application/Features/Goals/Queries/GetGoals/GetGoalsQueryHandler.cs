using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Goals.DTOs;
using CodeNexus.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Goals.Queries.GetGoals;

public class GetGoalsQueryHandler : IRequestHandler<GetGoalsQuery, Result<List<GoalDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetGoalsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<List<GoalDto>>> Handle(GetGoalsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();

        var goals = await _context.Goals
            .AsNoTracking()
            .Where(g => !g.IsDeleted &&
                       (g.IsSystemDefined || g.CreatedByUserId == userId))
            .OrderByDescending(g => g.IsSystemDefined)
            .ThenByDescending(g => g.CreatedAt)
            .Select(g => new GoalDto(
                g.GoalId,
                g.Title,
                g.Description,
                g.IsSystemDefined,
                g.Duration,
                g.DurationInDays,
                g.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return Result<List<GoalDto>>.Success(goals);
    }
}
