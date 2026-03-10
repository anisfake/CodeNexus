using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Quizzes.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Quizzes.Commands.SubmitQuizAttempt;

public record SubmitQuizAttemptCommand(
    Guid AttemptId,
    List<AnswerItemDto> Answers
) : IRequest<Result<SubmitQuizResultDto>>;
