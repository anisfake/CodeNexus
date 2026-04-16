using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.Tasks.DTOs;

public record TaskItemDto(
    Guid TaskId,
    string Title,
    string Description,
    DateTime? DueDate,
    TaskType TaskType,
    TaskPriority? Priority,
    TaskStatus_ TaskStatus,
    string? QuizQuestionsJson
);

public record ChapterTasksDto(
    Guid ChapterId,
    string ChapterTitle,
    List<TaskItemDto> Tasks
);
