using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Goals.DTOs;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Application.Features.Goals.Queries.GetMyGoal
{
    public class GetMyGoalQueryHandler : IRequestHandler<GetMyGoalQuery, Result<PaginationDto<GoalDto>>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        public GetMyGoalQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }
        public async Task<Result<PaginationDto<GoalDto>>> Handle(GetMyGoalQuery request, CancellationToken cancellationToken)
        {
            var userId = _currentUserService.GetUserId();

            var query = _context.Goals
                .Where(g => g.CreatedByUserId == userId && !g.IsDeleted)
                .AsQueryable();

            if (!string.IsNullOrEmpty(request.SearchTerm))
            {
                query = query.Where(x => x.Title.Contains(request.SearchTerm) || (x.Description != null && x.Description.Contains(request.SearchTerm)));
            }

            var totalCount = await query.CountAsync(cancellationToken);

            query = request.SortDescending
                ? query.OrderByDescending(lp => lp.CreatedAt)
                : query.OrderBy(lp => lp.CreatedAt);

            var items = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(g => new GoalDto(
                g.GoalId,
                g.Title,
                g.Description,
                g.IsSystemDefined,
                g.IsActive,
                g.Duration,
                g.DurationInDays,
                g.CreatedAt
            )).ToListAsync(cancellationToken);

            return Result<PaginationDto<GoalDto>>.Success(new PaginationDto<GoalDto>
            {
                Items = items,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                TotalCount = totalCount
            });
        }
    }
}
