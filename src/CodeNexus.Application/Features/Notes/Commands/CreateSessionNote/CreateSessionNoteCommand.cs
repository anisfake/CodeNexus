using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Notes.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Notes.Commands.CreateSessionNote;

public record CreateSessionNoteCommand(
    Guid SessionId,
    string? Title,
    string Content
) : IRequest<Result<SessionNoteResponse>>;
