using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Notes.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Notes.Queries.GetSessionNotes;

public record GetSessionNotesQuery(Guid SessionId) : IRequest<Result<List<SessionNoteResponse>>>;
