using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.FocusSessions.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.FocusSessions.Commands.ResumeSession;

public record ResumeSessionCommand(Guid SessionId) : IRequest<Result<ResumeSessionResponseDto>>;
