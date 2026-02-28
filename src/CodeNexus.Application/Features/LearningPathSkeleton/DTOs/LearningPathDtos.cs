using CodeNexus.Domain.Enums;

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
    Guid TaskId,
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

public record GenerateLearningPathSkeletonRequest(
    Guid SubjectId,
    Guid GoalId,
    ComplexityLevel ComplexityLevel
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

public record LearningPathResponse(
    Guid PathId,
    Guid SubjectId,
    string SubjectName,
    Guid GoalId,
    string GoalTitle,
    DateTime? StartDate,
    DateTime? EndDate,
    string Title,
    string Description,
    string Status,
    bool CreatedByType,
    Guid UserId,
    string UserName,
    List<ChapterDto> ChapterDtos,
    int? ChapterCount,
    DateTime CreatedAt
);
public record GetAllLearningPathRequest(
    int PageNumber = 1,
    int PageSize = 10,
    string? SearchTerm = null,
    Guid? SubjectId = null,
    LearningPathStatus? Status = null,
    bool SortDescending = true
);
public record GetLearningPathByUserIdRequest(
    int PageNumber = 1,
    int PageSize = 10,
    string? SearchTerm = null,
    Guid? SubjectId = null,
    LearningPathStatus? Status = null,
    bool SortDescending = true
);