namespace CodeNexus.Application.Features.Tasks.DTOs;

public record GeneratedTaskItemDto(
    string Title,
    string Description,
    string Priority,
    string TaskType,
    string? VerificationPrompt,
    int? MinimumScore,
    List<QuizQuestionDto>? QuizQuestions
);

public record QuizQuestionDto(
    string Question,
    List<string> Options,
    int CorrectAnswer
);

public record GeneratedTasksDto(
    List<GeneratedTaskItemDto> Tasks
);
