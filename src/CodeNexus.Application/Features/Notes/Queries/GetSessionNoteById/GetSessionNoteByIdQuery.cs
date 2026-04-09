using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Notes.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Notes.Queries.GetSessionNoteById;

public record GetSessionNoteByIdQuery(
    Guid SessionId,
    Guid NoteId
) : IRequest<Result<SessionNoteResponse>>;
