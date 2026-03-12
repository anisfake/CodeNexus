using CodeNexus.Application.Common.Models;
using MediatR;

namespace CodeNexus.Application.Features.FocusSessions.Commands.ReviewSession;

public record ReviewSessionCommand(
    Guid SessionId,
    string? SubmittedCode = null,
    string? SubmittedSummary = null,
    string? SubmittedQuizAnswers = null
) : IRequest<Result<ReviewSessionResponseDto>>;