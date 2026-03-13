namespace CodeNexus.Application.Features.Quizzes.DTOs;

public record QuizSkeletonDto(
    Guid QuizId,
    string Title,
    string? Description,
    int? TimeLimit,
    decimal? PassingScore
);

public record GeneratedQuizSkeletonDto(
    List<QuizSkeletonDto> Quizzes
);