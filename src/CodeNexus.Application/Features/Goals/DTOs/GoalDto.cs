using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.Goals.DTOs;

public record GoalDto(
    Guid GoalId,
    string Title,
    string? Description,
    bool IsSystemDefined,
    bool IsActive,
    GoalDuration Duration,
    int DurationInDays,
    DateTime CreatedAt
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
    string Title,
    string? Description,
    GoalDuration Duration
);

public record UpdateGoalRequest(
    string Title,
    string? Description,
    bool IsActive,
    GoalDuration Duration
);