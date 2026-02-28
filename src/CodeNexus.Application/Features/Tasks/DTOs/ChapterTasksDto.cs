using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.Tasks.DTOs;

public record TaskItemDto(
    Guid TaskId,
    string Title,
    string? Description,
    TaskPriority? Priority,
    TaskStatus_ Status
);

public record ChapterTasksDto(
    Guid ChapterId,
    string ChapterTitle,
    List<TaskItemDto> Tasks
);
