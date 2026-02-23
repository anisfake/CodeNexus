namespace CodeNexus.Application.Features.LearningPaths.DTOs;

public record QuizDto(
    Guid QuizzId,
    string Title,
    string Description
);
public record LessonDto(
    Guid LessonId,
    string Title,
    string? Content,
    List<QuizDto> Quizzes
);
public record TaskDto(
    Guid GoalId,
    string Title,
    string Description
);
public record ChapterDto(
    Guid ChapterId,
    string Title,
    string? Content,
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
    List<ChapterDto> ChapterDtos,
    int? ChapterCount,
    DateTime CreatedAt,
    bool IsContentGenerating = true
);

