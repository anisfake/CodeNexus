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
    public class GetMyGoalQueryHandler : IRequestHandler<GetMyGoalQuery, Result<PaginationDto<GetMyGoalGoalResponse>>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        public GetMyGoalQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }
        public async Task<Result<PaginationDto<GetMyGoalGoalResponse>>> Handle(GetMyGoalQuery request, CancellationToken cancellationToken)
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
            .Include(u => u.UserGoalProgresses)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(g => new GetMyGoalGoalResponse(
                g.GoalId,
                g.Title,
                g.Description,
                g.IsSystemDefined,
                g.Duration,
                g.DurationInDays,
                g.UserGoalProgresses.FirstOrDefault(ugp => ugp.GoalId == g.GoalId) != null 
                    ? g.UserGoalProgresses.FirstOrDefault(ugp => ugp.GoalId == g.GoalId)!.ProgressPercent 
                    : 0m,
                g.CreatedAt
            )).ToListAsync(cancellationToken);

            return Result<PaginationDto<GetMyGoalGoalResponse>>.Success(new PaginationDto<GetMyGoalGoalResponse>
            {
                Items = items,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                TotalCount = totalCount
            });
        }
    }
}
