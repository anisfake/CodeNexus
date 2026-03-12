using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.FocusSessions.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;

namespace CodeNexus.Application.Features.FocusSessions.Commands.StartSession;

public record StartSessionCommand(
    Guid TaskId,
    SessionType SessionType = SessionType.Pomodoro,
    int? PlannedDurationMinutes = null,
    string? Title = null) : IRequest<Result<StartSessionResponseDto>>;