using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.LearningPaths.DTOs;

public record GetAllLearningPathRequest(
    int PageNumber = 1,
    int PageSize = 10,
    string? SearchTerm = null,
    Guid? SubjectId = null,
    LearningPathStatus? Status = null,
    bool SortDescending = true
);
