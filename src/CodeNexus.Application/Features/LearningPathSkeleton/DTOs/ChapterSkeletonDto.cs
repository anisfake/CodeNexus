namespace CodeNexus.Application.Features.LearningPathSkeleton.DTOs;

public record ChapterSkeletonDto(
    Guid ChapterId,
    string Title,
    int OrderIndex,
    int LessonCount,
    int QuizCount
);
