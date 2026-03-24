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
    bool IsActive,
    int DurationInDays
);

public record CreateSubjectRequest(
    string Name,
    string? Description,
    string? Color,
    string? Icon,
    SubjectCategory Category
);

public record UpdateSubjectRequest(
    string Name,
    string? Description,
    string? Color,
    string? Icon,
    SubjectCategory Category
);
