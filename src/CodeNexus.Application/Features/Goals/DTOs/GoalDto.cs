namespace CodeNexus.Application.Features.Goals.DTOs;

public record GoalDto(
    Guid GoalId,
    string Title,
    string? Description,
    bool IsSystemDefined,
    bool IsActive,
    DateTime CreatedAt
);

public record CreateGoalResponseDto(
    Guid GoalId,
    string Title,
    string? Description,
    bool IsSystemDefined
);

public record CreateGoalRequest(
    string Title,
    string? Description
);

public record UpdateGoalRequest(
    string Title,
    string? Description,
    bool IsActive
);