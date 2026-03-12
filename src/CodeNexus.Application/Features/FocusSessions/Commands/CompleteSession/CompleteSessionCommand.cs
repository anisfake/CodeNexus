using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.FocusSessions.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;

namespace CodeNexus.Application.Features.FocusSessions.Commands.CompleteSession;

public record CompleteSessionCommand(
    Guid SessionId,
    string? SubmittedCode = null,
    string? SubmittedSummary = null,
    string? SubmittedQuizAnswers = null,
    bool IsEarlyCompletion = false,
    SubmissionType SubmissionType = SubmissionType.Progress) : IRequest<Result<CompleteSessionResponseDto>>;