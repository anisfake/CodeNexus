using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Goals.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Goals.Queries.GetGoalDashboard;

public class GetGoalDashboardQueryHandler : IRequestHandler<GetGoalDashboardQuery, Result<GoalDashboardResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetGoalDashboardQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<GoalDashboardResponseDto>> Handle(GetGoalDashboardQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();
        var normalizedSearch = request.SearchTerm?.Trim();

        var personalGoalsQuery = _context.Goals
            .Where(g => !g.IsDeleted && !g.IsSystemDefined && g.CreatedByUserId == userId);

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            personalGoalsQuery = personalGoalsQuery.Where(g =>
                g.Title.Contains(normalizedSearch) ||
                (g.Description != null && g.Description.Contains(normalizedSearch)));
        }

        var personalGoals = await personalGoalsQuery
            .OrderByDescending(g => g.CreatedAt)
            .Select(g => new
            {
                g.GoalId,
                g.Title,
                g.Description
            })
            .ToListAsync(cancellationToken);

        var personalGoalIds = personalGoals.Select(g => g.GoalId).ToList();
        var personalProgressRows = personalGoalIds.Count == 0
            ? new List<PersonalProgressRow>()
            : await _context.UserGoalProgresses
                .Where(x => x.UserId == userId && personalGoalIds.Contains(x.GoalId))
                .Select(x => new PersonalProgressRow(
                    x.GoalId,
                    x.ProgressPercent,
                    x.LastUpdatedAt))
                .ToListAsync(cancellationToken);

        var personalProgressLookup = personalProgressRows
            .GroupBy(x => x.GoalId)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    Progress = Math.Round(Math.Clamp(g.Sum(x => x.ProgressPercent), 0m, 100m), 2),
                    LastUpdatedAt = g.Max(x => x.LastUpdatedAt)
                });

        var personalDtos = personalGoals.Select(goal =>
        {
            personalProgressLookup.TryGetValue(goal.GoalId, out var progressData);
            var progressPercent = progressData?.Progress ?? 0m;
            var status = progressPercent switch
            {
                <= 0m => GoalProgressStatus.NotStarted.ToString(),
                >= 100m => GoalProgressStatus.Completed.ToString(),
                _ => GoalProgressStatus.InProgress.ToString()
            };

            return new GoalDashboardPersonalGoalDto(
                goal.GoalId,
                goal.Title,
                goal.Description,
                progressPercent,
                status,
                progressData?.LastUpdatedAt);
        }).ToList();

        var learningPathStatuses = request.PathStatus.HasValue
            ? new[] { request.PathStatus.Value.ToString() }
            : new[] { LearningPathStatus.Active.ToString(), LearningPathStatus.InProgress.ToString() };

        var pathGoalsQuery =
            from lp in _context.LearningPaths
            join lpg in _context.LearningPathGoals on lp.PathId equals lpg.PathId
            join goal in _context.Goals on lpg.GoalId equals goal.GoalId
            join subject in _context.Subjects on lp.SubjectId equals subject.SubjectId
            join ugp in _context.UserGoalProgresses.Where(x => x.UserId == userId)
                on new { PathId = lp.PathId, GoalId = goal.GoalId }
                equals new { PathId = ugp.LearningPathId, GoalId = ugp.GoalId } into ugpJoin
            from ugp in ugpJoin.OrderByDescending(x => x.LastUpdatedAt).Take(1).DefaultIfEmpty()
            where lp.UserId == userId
                  && learningPathStatuses.Contains(lp.Status)
                  && !goal.IsDeleted
            select new
            {
                lp,
                lpg,
                goal,
                subject,
                ugp
            };

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            pathGoalsQuery = pathGoalsQuery.Where(x =>
                x.lp.Title.Contains(normalizedSearch) ||
                x.goal.Title.Contains(normalizedSearch) ||
                (x.goal.Description != null && x.goal.Description.Contains(normalizedSearch)) ||
                x.subject.Name.Contains(normalizedSearch));
        }

        var totalPathGoalCount = await pathGoalsQuery.CountAsync(cancellationToken);

        pathGoalsQuery = request.SortDescending
            ? pathGoalsQuery.OrderByDescending(x => x.lp.CreatedAt).ThenByDescending(x => x.lpg.Weight)
            : pathGoalsQuery.OrderBy(x => x.lp.CreatedAt).ThenByDescending(x => x.lpg.Weight);

        var pageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber;
        var pageSize = request.PageSize <= 0 ? 20 : request.PageSize;

        var rawPathGoals = await pathGoalsQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new RawPathGoalRow(
                x.lp.PathId,
                x.lp.Title,
                x.lp.Status,
                x.lp.CreatedAt,
                x.subject.SubjectId,
                x.subject.Name,
                x.goal.GoalId,
                x.goal.Title,
                x.goal.Description,
                x.goal.IsSystemDefined,
                x.lpg.Weight,
                x.ugp != null ? x.ugp.ProgressPercent : 0m,
                x.ugp != null ? (GoalProgressStatus?)x.ugp.Status : null,
                x.ugp != null ? x.ugp.CompletedAt : null,
                x.ugp != null ? x.ugp.LastUpdatedAt : null))
            .ToListAsync(cancellationToken);

        var pathGoalDtos = rawPathGoals.Select(x =>
        {
            var targetPercent = Math.Round(Math.Clamp(x.Weight, 0m, 1m) * 100m, 2);
            var progressPercent = Math.Round(Math.Clamp(x.ProgressPercent, 0m, 100m), 2);
            var completionPercent = targetPercent <= 0m
                ? 0m
                : Math.Round(Math.Clamp((progressPercent / targetPercent) * 100m, 0m, 100m), 2);

            return new GoalDashboardPathGoalDto(
                x.LearningPathId,
                x.LearningPathTitle,
                x.LearningPathStatus,
                x.SubjectId,
                x.SubjectName,
                x.GoalId,
                x.GoalTitle,
                x.GoalDescription,
                x.IsSystemDefined,
                x.Weight,
                targetPercent,
                progressPercent,
                completionPercent,
                x.GoalStatus?.ToString() ?? GoalProgressStatus.NotStarted.ToString(),
                x.CompletedAt,
                x.LastUpdatedAt);
        }).ToList();

        var pagedPathGoals = new PaginationDto<GoalDashboardPathGoalDto>
        {
            Items = pathGoalDtos,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalPathGoalCount
        };

        var response = new GoalDashboardResponseDto(personalDtos, pagedPathGoals);
        return Result<GoalDashboardResponseDto>.Success(response);
    }

    private sealed record PersonalProgressRow(
        Guid GoalId,
        decimal ProgressPercent,
        DateTime LastUpdatedAt);

    private sealed record RawPathGoalRow(
        Guid LearningPathId,
        string LearningPathTitle,
        string LearningPathStatus,
        DateTime LearningPathCreatedAt,
        Guid SubjectId,
        string SubjectName,
        Guid GoalId,
        string GoalTitle,
        string? GoalDescription,
        bool IsSystemDefined,
        decimal Weight,
        decimal ProgressPercent,
        GoalProgressStatus? GoalStatus,
        DateTime? CompletedAt,
        DateTime? LastUpdatedAt);
}
