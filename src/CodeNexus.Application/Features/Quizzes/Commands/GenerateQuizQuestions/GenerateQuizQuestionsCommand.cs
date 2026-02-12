using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Quizzes.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Quizzes.Commands.GenerateQuizQuestions;

public record GenerateQuizQuestionsCommand(Guid QuizId) : IRequest<Result<QuizQuestionsDto>>;
