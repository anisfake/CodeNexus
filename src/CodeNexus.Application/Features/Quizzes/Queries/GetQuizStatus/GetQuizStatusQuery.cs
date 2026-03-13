using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Quizzes.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Quizzes.Queries.GetQuizStatus;

public record GetQuizStatusQuery(Guid QuizId) : IRequest<Result<QuizStatusDto>>;
