using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Quizzes.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Quizzes.Commands.StartQuizAttempt;

public record StartQuizAttemptCommand(Guid QuizId) : IRequest<Result<StartQuizAttemptDto>>;
