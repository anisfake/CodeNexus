namespace CodeNexus.Application.Features.LearningPaths.DTOs;

public record QuizDto(
    string Title,
    string Description
);
public record LessonDto(
    string Title,
    string Description,
    List<QuizDto> Quizzes
);
public record TaskDto(
    string Title,
    string Description
);
public record ChapterDto(
    string Title,
    string Description,
    int OrderIndex,
    List<LessonDto> Lessons,
    List<TaskDto> Tasks
);
public record LearningPathSkeletonDto(
    string Title,
    string Description,
    List<ChapterDto> Chapters
);
public record CreateLearningPathResponse(
    Guid PathId,
    string Title,
    string Description,
    int ChapterCount,
    DateTime CreatedAt
);
