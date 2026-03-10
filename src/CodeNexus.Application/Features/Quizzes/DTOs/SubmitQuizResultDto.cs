using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.Quizzes.DTOs;

public record AnswerItemDto(Guid QuestionId, string Answer);

public record QuestionResultDto(
    Guid QuestionId,
    string QuestionText,
    QuestionType Type,
    List<string> Options,
    string UserAnswer,
    string CorrectAnswer,
    bool IsCorrect,
    decimal Points,
    decimal EarnedPoints
);

public record SubmitQuizResultDto(
    Guid AttemptId,
    Guid QuizId,
    decimal Score,
    decimal TotalPoints,
    decimal Percentage,
    bool Passed,
    DateTime StartTime,
    DateTime EndTime,
    List<QuestionResultDto> QuestionResults
);
