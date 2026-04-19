using CodeNexus.Domain.Enums;
using CodeNexus.Application.Common.Models;

namespace CodeNexus.Application.Features.Goals.DTOs;

public record GoalDto(
    Guid GoalId,
    string Title,
    string? Description,
    bool IsSystemDefined,
    GoalDuration Duration,
    int DurationInDays,
    DateTime CreatedAt
);

public record GetMyGoalGoalResponse(
    Guid GoalId,
    string Title,
    string? Description,
    bool IsSystemDefined,
    GoalDuration Duration,
    int DurationInDays,
    decimal? ProgressPercentage,
    DateTime CreatedAt
);

public record GoalMappingDto(
    Guid UserGoalId,
    Guid SystemGoalId,
    decimal Confidence,
    bool VerifiedByAI,
    DateTime CreatedAt,
    string SystemGoalTitle,
    string? SystemGoalDescription
);

public record CreateGoalResponseDto(
    Guid GoalId,
    string Title,
    string? Description,
    bool IsSystemDefined,
    GoalDuration Duration,
    int DurationInDays
);

public record CreateGoalRequest(
    Guid SubjectId,
    string Title,
    string? Description,
    GoalDuration Duration
);

public record UpdateGoalRequest(
    Guid SubjectId,
    string Title,
    string? Description,
    GoalDuration Duration
);

public record GoalDashboardPersonalGoalDto(
    Guid GoalId,
    string Title,
    string? Description,
    decimal ProgressPercent,
    string Status,
    DateTime? LastUpdatedAt
);

public record GoalDashboardPathGoalDto(
    Guid LearningPathId,
    string LearningPathTitle,
    string LearningPathStatus,
    Guid SubjectId,
    string SubjectName,
    Guid GoalId,
    string GoalTitle,
    string? GoalDescription,
    bool IsSystemDefined,
    decimal Weight,
    decimal TargetPercent,
    decimal ProgressPercent,
    decimal CompletionPercent,
    string GoalStatus,
    DateTime? CompletedAt,
    DateTime? LastUpdatedAt
);

public record GoalDashboardResponseDto(
    List<GoalDashboardPersonalGoalDto> PersonalGoals,
    PaginationDto<GoalDashboardPathGoalDto> PathGoals
);

public record GetGoalDashboardRequest(
    int PageNumber = 1,
    int PageSize = 20,
    string? SearchTerm = null,
    LearningPathStatus? PathStatus = null,
    bool SortDescending = true
);
