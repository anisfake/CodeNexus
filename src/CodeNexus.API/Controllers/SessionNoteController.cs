using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Notes.Commands.CreateSessionNote;
using CodeNexus.Application.Features.Notes.Commands.DeleteSessionNote;
using CodeNexus.Application.Features.Notes.Commands.UpdateSessionNote;
using CodeNexus.Application.Features.Notes.DTOs;
using CodeNexus.Application.Features.Notes.Queries.GetSessionNoteById;
using CodeNexus.Application.Features.Notes.Queries.GetSessionNotes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeNexus.API.Controllers;

[ApiController]
[Authorize]
public class SessionNoteController : ControllerBase
{
    private readonly ISender _sender;

    public SessionNoteController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("api/focus-sessions/{sessionId:guid}/notes")]
    public async Task<IActionResult> GetSessionNotes(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetSessionNotesQuery(sessionId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("api/focus-sessions/{sessionId:guid}/notes/{noteId:guid}")]
    public async Task<IActionResult> GetSessionNoteById(Guid sessionId, Guid noteId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetSessionNoteByIdQuery(sessionId, noteId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("api/focus-sessions/{sessionId:guid}/notes")]
    public async Task<IActionResult> CreateSessionNote(
        Guid sessionId,
        [FromBody] CreateSessionNoteRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateSessionNoteCommand(sessionId, request.Title, request.Content);
        var result = await _sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut("api/focus-sessions/{sessionId:guid}/notes/{noteId:guid}")]
    public async Task<IActionResult> UpdateSessionNote(
        Guid sessionId,
        Guid noteId,
        [FromBody] UpdateSessionNoteRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateSessionNoteCommand(sessionId, noteId, request.Title, request.Content);
        var result = await _sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    [HttpDelete("api/focus-sessions/{sessionId:guid}/notes/{noteId:guid}")]
    public async Task<IActionResult> DeleteSessionNote(
        Guid sessionId,
        Guid noteId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeleteSessionNoteCommand(sessionId, noteId), cancellationToken);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return result.ErrorCode switch
        {
            "SESSION_NOT_FOUND" or "NOTE_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
            "NOTE_CONTENT_REQUIRED" => BadRequest(new { result.ErrorCode, result.ErrorMessage }),
            _ => StatusCode(StatusCodes.Status500InternalServerError, new { result.ErrorCode, result.ErrorMessage })
        };
    }
}
