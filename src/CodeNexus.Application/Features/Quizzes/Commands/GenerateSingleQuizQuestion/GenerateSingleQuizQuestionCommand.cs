using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Quizzes.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;

namespace CodeNexus.Application.Features.Quizzes.Commands.GenerateSingleQuizQuestion;

public record GenerateSingleQuizQuestionCommand(Guid QuizId, QuestionType QuestionType) : IRequest<Result<QuestionItemDto>>;
