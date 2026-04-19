using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.LearningPaths.DTOs;

public record QuizDto(
    Guid QuizzId,
    string Title,
    string Description,
    List<QuestionDto>? Questions = null,
    string Status = "Not Attempted"
);

public record QuestionDto(
    Guid QuestionId,
    string QuestionText,
    QuestionType Type,
    List<string> Options,
    string CorrectAnswer,
    decimal Points,
    int OrderIndex
);
public record LessonDto(
    Guid LessonId,
    string Title,
    string? Content,
    DateTime LessonDay,
    List<QuizDto> Quizzes,
    string Status = "Not Started"
);
public record TaskDto(
    Guid TaskId,
    string Title,
    string Description,
    TaskType TaskType,
    TaskPriority? Priority,
    TaskStatus_ TaskStatus,
    DateTime? DueDate,
    string? QuizQuestionsJson,
    string Status = "Pending"
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
    int DurationInDays,
    string Status,
    DateTime? CompletedAt,
    decimal ProgressPercent = 0m,
    decimal TargetPercent = 100m
);

public record GenerateLearningPathSkeletonRequest(
    Guid SubjectId,
    List<LearningPathGoalRequest> Goals,
    ComplexityLevel ComplexityLevel,
    LanguageSelection LanguageSelection,
    bool SaveAsDraft = false
);

public record AdoptSuggestedLearningPathRequest(
    Guid SubjectId,
    List<LearningPathGoalRequest> Goals,
    ComplexityLevel ComplexityLevel,
    LanguageSelection LanguageSelection
);

public record GenerateChapterMentorSkeletonRequest(
    string ChapterTitle,
    string? ChapterDescription = null
);

public record ManualLessonRequest(
    string Title,
    DateTime LessonDay,
    List<ManualQuizRequest>? Quizzes = null,
    string? Content = null
);

public record ManualQuizRequest(
    string Title,
    string? Description = null,
    DateTime? DueDate = null,
    List<ManualQuestionRequest>? Questions = null
);

public record ManualQuestionRequest(
    string QuestionText,
    QuestionType Type,
    List<string>? Options = null,
    string? CorrectAnswer = null,
    decimal Points = 1
);

public record ManualTaskRequest(
    string Title,
    string? Description = null,
    TaskType TaskType = TaskType.Practice,
    TaskPriority? Priority = null,
    DateTime? DueDate = null,
    string? QuizQuestionsJson = null
);

public record ManualChapterRequest(
    string Title,
    DateTime? StartDate,
    DateTime? EndDate,
    int? EstimatedDays,
    List<ManualLessonRequest> Lessons,
    List<ManualTaskRequest>? Tasks = null
);

public record CreateMentorLearningPathDraftRequest(
    Guid SubjectId,
    List<LearningPathGoalRequest> Goals,
    ComplexityLevel ComplexityLevel,
    LanguageSelection LanguageSelection,
    string Title,
    string? Description,
    DateTime StartDate,
    DateTime EndDate,
    List<ManualChapterRequest> Chapters
);

public enum DraftVersionUpdateType
{
    Minor = 0,
    Major = 1
}

public record UpdateMentorLearningPathDraftRequest(
    bool IncreaseVersion,
    DraftVersionUpdateType? VersionUpdateType,
    Guid SubjectId,
    List<LearningPathGoalRequest> Goals,
    ComplexityLevel ComplexityLevel,
    LanguageSelection LanguageSelection,
    string Title,
    string? Description,
    DateTime StartDate,
    DateTime EndDate,
    List<ManualChapterRequest> Chapters
);

public record CreateLearningPathResponse(
    Guid PathId,
    string Title,
    string Description,
    List<LearningPathGoalDto> Goals,
    List<ChapterDto> ChapterDtos,
    int? ChapterCount,
    DateTime CreatedAt,
    bool IsContentGenerating = true,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    ComplexityLevel? ComplexityLevel = null,
    LanguageSelection? LanguageSelection = null,
    Guid? SubjectId = null,
    string? SubjectName = null,
    decimal? Version = null,
    decimal? PreviousVersion = null,
    bool? HasMeaningfulChange = null
)
{
    public List<ChapterDto> Chapters => ChapterDtos;
}

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
    DateTime CreatedAt,
    ComplexityLevel? ComplexityLevel,
    LanguageSelection? LanguageSelection,
    Guid? SharedByUserId,
    string? SharedByUserName,
    Guid? SourceLearningPathId,
    decimal? SourceVersion,
    decimal? SourceLatestVersion,
    bool HasSourceUpdate
)
{
    public List<ChapterDto> Chapters => ChapterDtos;

    public LearningPathResponse(
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
        DateTime CreatedAt,
        ComplexityLevel? ComplexityLevel,
        LanguageSelection? LanguageSelection)
        : this(
            PathId,
            SubjectId,
            SubjectName,
            Goals,
            StartDate,
            EndDate,
            Title,
            Description,
            Status,
            CreatedByType,
            UserId,
            UserName,
            ChapterDtos,
            ChapterCount,
            CreatedAt,
            ComplexityLevel,
            LanguageSelection,
            null,
            null,
            null,
            null,
            null,
            false)
    {
    }
}
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

public record GetMyLearningPathDraftsRequest(
    int PageNumber = 1,
    int PageSize = 10,
    string? SearchTerm = null,
    Guid? SubjectId = null,
    bool SortDescending = true
);
