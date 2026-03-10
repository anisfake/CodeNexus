using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.FocusSessions.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.FocusSessions.Commands.CompleteSession;

public record CompleteSessionCommand(
    Guid SessionId,
    string? SubmittedCode = null,
    string? SubmittedSummary = null,
    bool IsEarlyCompletion = false) : IRequest<Result<CompleteSessionResponseDto>>;