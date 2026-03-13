namespace CodeNexus.Application.Features.Quizzes.DTOs;

public record QuizStatusDto(
    Guid QuizId,
    string Title,
    int? TimeLimit,
    decimal? PassingScore,
    int TotalQuestions,
    string Status,
    Guid? LastAttemptId,
    decimal? LastScore,
    DateTime? LastAttemptDate
);
