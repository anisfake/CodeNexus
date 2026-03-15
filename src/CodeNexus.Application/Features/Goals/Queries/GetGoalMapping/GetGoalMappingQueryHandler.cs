using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Goals.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Goals.Queries.GetGoalMapping;

public class GetGoalMappingQueryHandler : IRequestHandler<GetGoalMappingQuery, Result<GoalMappingDto?>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetGoalMappingQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<GoalMappingDto?>> Handle(GetGoalMappingQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();

        var goal = await _context.Goals
            .AsNoTracking()
            .FirstOrDefaultAsync(g =>
                g.GoalId == request.GoalId
                && !g.IsDeleted
                && (g.IsSystemDefined || g.CreatedByUserId == userId),
                cancellationToken);

        if (goal == null)
        {
            return Result<GoalMappingDto?>.Failure("GOAL_NOT_FOUND", "Goal not found");
        }

        if (goal.IsSystemDefined)
        {
            return Result<GoalMappingDto?>.Success(null);
        }

        var mapping = await _context.GoalMappings
            .AsNoTracking()
            .Where(m => m.UserGoalId == request.GoalId)
            .Join(_context.Goals,
                m => m.SystemGoalId,
                g => g.GoalId,
                (m, g) => new GoalMappingDto(
                    m.UserGoalId,
                    m.SystemGoalId,
                    m.Confidence,
                    m.VerifiedByAI,
                    m.CreatedAt,
                    g.Title,
                    g.Description))
            .FirstOrDefaultAsync(cancellationToken);

        return Result<GoalMappingDto?>.Success(mapping);
    }
}
