namespace CodeNexus.Application.Features.LearningPathSkeleton.DTOs;

public record LearningPathSkeletonDto(
    Guid PathId,
    string Title,
    string Description,
    int ChapterCount,
    List<ChapterDto> ChapterDtos
);

public record ChapterDto(
    Guid ChapterId,
    string Title,
    int OrderIndex,
    int LessonCount = 0
);