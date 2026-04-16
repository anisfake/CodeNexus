using CodeNexus.Domain.Enums;

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
