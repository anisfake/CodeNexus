using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Notes.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Notes.Commands.UpdateSessionNote;

public record UpdateSessionNoteCommand(
    Guid SessionId,
    Guid NoteId,
    string? Title,
    string Content
) : IRequest<Result<SessionNoteResponse>>;
