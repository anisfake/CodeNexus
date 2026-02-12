using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.Quizzes.DTOs;

public record QuestionItemDto(
    Guid QuestionId,
    string QuestionText,
    QuestionType Type,
    List<string> Options,
    string CorrectAnswer,
    decimal Points,
    int OrderIndex
);

public record QuizQuestionsDto(
    Guid QuizId,
    string Title,
    List<QuestionItemDto> Questions
);
