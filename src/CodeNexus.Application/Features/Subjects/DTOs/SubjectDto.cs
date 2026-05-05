using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.Subjects.DTOs;

public record SubjectDto(
    Guid SubjectId,
    string Name,
    string? Description,
    string? Color,
    string? Icon,
    SubjectCategory Category,
    List<SubjectGoalDto> Goals,
    string CreatedBy,
    Guid CreatedByUserId,
    DateTime CreatedAt
);

public record SubjectGoalDto(
    Guid GoalId,
    string Title,
    string? Description,
    bool IsSystemDefined,
    int DurationInDays
);

public record CreateSubjectGoalRequest(
    string Title,
    string? Description,
    GoalDuration Duration
);

public record UpdateSubjectGoalRequest(
    Guid? GoalId,
    string Title,
    string? Description,
    GoalDuration Duration
);

public record CreateSubjectRequest(
    string Name,
    string? Description,
    string? Color,
    string? Icon,
    SubjectCategory Category,
    List<CreateSubjectGoalRequest>? Goals
);

public record UpdateSubjectRequest(
    string Name,
    string? Description,
    string? Color,
    string? Icon,
    SubjectCategory Category,
    List<UpdateSubjectGoalRequest>? Goals
);
