using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Notes.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Notes.Commands.DeleteSessionNote;

public record DeleteSessionNoteCommand(
    Guid SessionId,
    Guid NoteId
) : IRequest<Result<DeleteSessionNoteResponse>>;
