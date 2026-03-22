namespace CodeNexus.Application.Features.Lessons.DTOs;

public record LessonReadStatusDto(
    Guid LessonId,
    bool IsLessonContentRead,
    DateTime? ReadAt
);
