namespace CodeNexus.Application.Features.Goals.DTOs;

public record GoalDto(
    Guid GoalId,
    string Title,
    string? Description,
    int DurationDays,
    bool IsCompleted,
    DateTime? CompletedAt,
    DateTime CreatedAt
);
public record CreateGoalResponeDto(
    string Title,
    string? Description,
    int DurationDays
);

public record CreateGoalRequest(
    string Title,
    string? Description,
    int DurationDays
);