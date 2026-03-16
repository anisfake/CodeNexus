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
    DateTime LessonDay,
    List<QuizDto> Quizzes
);
public record TaskDto(
    Guid TaskId,
    string Title,
    string Description,
    TaskType TaskType,
    TaskPriority? Priority,
    TaskStatus_ TaskStatus,
    DateTime? DueDate,
    string? QuizQuestionsJson
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

public record LearningPathGoalRequest(
    Guid GoalId,
    decimal Weight
);

public record LearningPathGoalDto(
    Guid GoalId,
    string Title,
    decimal Weight,
    int DurationInDays
);

public record GenerateLearningPathSkeletonRequest(
    Guid SubjectId,
    List<LearningPathGoalRequest> Goals,
    ComplexityLevel ComplexityLevel,
    LanguageSelection LanguageSelection
);
public record CreateLearningPathResponse(
    Guid PathId,
    string Title,
    string Description,
    List<LearningPathGoalDto> Goals,
    List<ChapterDto> ChapterDtos,
    int? ChapterCount,
    DateTime CreatedAt,
    bool IsContentGenerating = true
);

public record LearningPathSuggestionDto(
    Guid PathId,
    string Title,
    string Description,
    decimal Score,
    List<LearningPathGoalDto> Goals,
    int? ChapterCount
);

public record LearningPathResponse(
    Guid PathId,
    Guid SubjectId,
    string SubjectName,
    List<LearningPathGoalDto> Goals,
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
