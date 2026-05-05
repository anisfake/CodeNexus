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
            : new[]
            {
                LearningPathStatus.Active.ToString(),
                LearningPathStatus.InProgress.ToString(),
                LearningPathStatus.Completed.ToString()
            };

        var pathGoalsQuery =
            from lp in _context.LearningPaths
            join lpg in _context.LearningPathGoals on lp.PathId equals lpg.PathId
            join goal in _context.Goals on lpg.GoalId equals goal.GoalId
            join subject in _context.Subjects on lp.SubjectId equals subject.SubjectId
            where lp.UserId == userId
                  && learningPathStatuses.Contains(lp.Status)
                  && !goal.IsDeleted
            select new
            {
                LearningPathId = lp.PathId,
                LearningPathTitle = lp.Title,
                LearningPathStatus = lp.Status,
                LearningPathCreatedAt = lp.CreatedAt,
                SubjectId = subject.SubjectId,
                SubjectName = subject.Name,
                GoalId = goal.GoalId,
                GoalTitle = goal.Title,
                GoalDescription = goal.Description,
                IsSystemDefined = goal.IsSystemDefined,
                Weight = lpg.Weight
            };

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            pathGoalsQuery = pathGoalsQuery.Where(x =>
                x.LearningPathTitle.Contains(normalizedSearch) ||
                x.GoalTitle.Contains(normalizedSearch) ||
                (x.GoalDescription != null && x.GoalDescription.Contains(normalizedSearch)) ||
                x.SubjectName.Contains(normalizedSearch));
        }

        var totalPathGoalCount = await pathGoalsQuery.CountAsync(cancellationToken);

        pathGoalsQuery = request.SortDescending
            ? pathGoalsQuery.OrderByDescending(x => x.LearningPathCreatedAt).ThenByDescending(x => x.Weight)
            : pathGoalsQuery.OrderBy(x => x.LearningPathCreatedAt).ThenByDescending(x => x.Weight);

        var pageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber;
        var pageSize = request.PageSize <= 0 ? 20 : request.PageSize;

        var pagePathGoals = await pathGoalsQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var pathGoalProgressLookup = new Dictionary<(Guid LearningPathId, Guid GoalId), PathGoalProgressRow>();
        if (pagePathGoals.Count > 0)
        {
            var pagePathIds = pagePathGoals.Select(x => x.LearningPathId).Distinct().ToList();
            var pageGoalIds = pagePathGoals.Select(x => x.GoalId).Distinct().ToList();

            var progressRows = await _context.UserGoalProgresses
                .Where(x =>
                    x.UserId == userId &&
                    pagePathIds.Contains(x.LearningPathId) &&
                    pageGoalIds.Contains(x.GoalId))
                .Select(x => new PathGoalProgressRow(
                    x.LearningPathId,
                    x.GoalId,
                    x.ProgressPercent,
                    x.Status,
                    x.CompletedAt,
                    x.LastUpdatedAt))
                .ToListAsync(cancellationToken);

            pathGoalProgressLookup = progressRows
                .GroupBy(x => new { x.LearningPathId, x.GoalId })
                .ToDictionary(
                    g => (g.Key.LearningPathId, g.Key.GoalId),
                    g => g.OrderByDescending(x => x.LastUpdatedAt).First());
        }

        var pathGoalDtos = pagePathGoals.Select(x =>
        {
            pathGoalProgressLookup.TryGetValue((x.LearningPathId, x.GoalId), out var progressRow);

            var targetPercent = Math.Round(Math.Clamp(x.Weight, 0m, 1m) * 100m, 2);
            var progressPercent = Math.Round(Math.Clamp(progressRow?.ProgressPercent ?? 0m, 0m, 100m), 2);
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
                progressRow?.GoalStatus.ToString() ?? GoalProgressStatus.NotStarted.ToString(),
                progressRow?.CompletedAt,
                progressRow?.LastUpdatedAt);
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

    private sealed record PathGoalProgressRow(
        Guid LearningPathId,
        Guid GoalId,
        decimal ProgressPercent,
        GoalProgressStatus GoalStatus,
        DateTime? CompletedAt,
        DateTime LastUpdatedAt);
}
