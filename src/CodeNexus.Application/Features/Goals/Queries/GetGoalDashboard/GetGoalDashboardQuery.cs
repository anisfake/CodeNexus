using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Goals.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;

namespace CodeNexus.Application.Features.Goals.Queries.GetGoalDashboard;

public record GetGoalDashboardQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? SearchTerm = null,
    LearningPathStatus? PathStatus = null,
    bool SortDescending = true
) : IRequest<Result<GoalDashboardResponseDto>>;
