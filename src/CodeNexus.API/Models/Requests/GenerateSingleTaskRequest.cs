using CodeNexus.Domain.Enums;

namespace CodeNexus.API.Models.Requests;

public record GenerateSingleTaskRequest(
    Guid ChapterId,
    string? Title,
    TaskType TaskType
);
