namespace CodeNexus.Application.Features.Tasks.DTOs;

public record GeneratedTaskItemDto(
    string Title,
    string Description,
    string Priority
);

public record GeneratedTasksDto(
    List<GeneratedTaskItemDto> Tasks
);
