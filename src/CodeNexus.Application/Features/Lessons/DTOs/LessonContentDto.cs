namespace CodeNexus.Application.Features.Lessons.DTOs;

public record LessonContentDto(
    Guid LessonId,
    string Title,
    string Content
);
