using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.Quizzes.DTOs;

public record GeneratedQuestionDto(
    string QuestionText,
    QuestionType Type,
    List<string> Options,
    string CorrectAnswer,
    decimal Points
);

public record GeneratedQuestionsDto(
    List<GeneratedQuestionDto> Questions
);
