using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.FocusSessions.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.FocusSessions.Commands.StartSession;

public record StartSessionCommand(
    Guid TaskId,
    int PlannedDurationMinutes = 25,
    string? Title = null) : IRequest<Result<StartSessionResponseDto>>;