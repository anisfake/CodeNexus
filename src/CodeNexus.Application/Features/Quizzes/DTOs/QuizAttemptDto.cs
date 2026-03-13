using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.Quizzes.DTOs;

public record AttemptQuestionDto(
    Guid QuestionId,
    string QuestionText,
    QuestionType Type,
    List<string> Options,
    decimal Points,
    int OrderIndex
);

public record StartQuizAttemptDto(
    Guid AttemptId,
    Guid QuizId,
    string Title,
    int? TimeLimit,
    decimal? PassingScore,
    int RemainingSeconds,
    DateTime StartTime,
    List<AttemptQuestionDto> Questions
);
