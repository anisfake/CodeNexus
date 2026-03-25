namespace CodeNexus.Application.Features.Timeline.DTOs;

public record StudentTimelineItemDto(
    Guid ItemId,
    string ItemType,
    string Title,
    DateTime DueAtUtc,
    Guid LearningPathId,
    string LearningPathTitle,
    Guid? ChapterId,
    string? ChapterTitle,
    Guid? LessonId,
    string Status,
    bool IsCompleted,
    bool IsOverdue,
    int? Priority);

public record StudentTimelineResponse(
    DateTime FromUtc,
    DateTime ToUtc,
    int TotalItems,
    int OverdueItems,
    List<StudentTimelineItemDto> Items);

