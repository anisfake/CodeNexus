namespace CodeNexus.Application.Features.Subjects.DTOs;

public record SubjectDto(
    Guid SubjectId,
    string Name,
    string? Description,
    string? Color,
    string? Icon,
    DateTime CreatedAt
);
