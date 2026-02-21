namespace CodeNexus.Application.Features.Subjects.DTOs;

public record SubjectDto(
    Guid SubjectId,
    string Name,
    string? Description,
    string? Color,
    string? Icon,
    DateTime CreatedAt
);

public record CreateSubjectRequest(
    string Name,
    string? Description,
    string? Color,
    string? Icon
);

public record UpdateSubjectRequest(
    string Name,
    string? Description,
    string? Color,
    string? Icon
);
