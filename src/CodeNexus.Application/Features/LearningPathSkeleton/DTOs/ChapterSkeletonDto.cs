namespace CodeNexus.Application.Features.LearningPathSkeleton.DTOs;

public record ChapterSkeletonDto(
    Guid ChapterId,
    string Title,
    int OrderIndex,
    int LessonCount,
    int QuizCount,
    List<LessonSkeletonDto> Lessons
);

public record LessonSkeletonDto(
    Guid LessonId,
    string Title,
    int OrderIndex,
    DateTime LessonDay
);
