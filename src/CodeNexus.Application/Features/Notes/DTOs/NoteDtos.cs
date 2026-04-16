namespace CodeNexus.Application.Features.Notes.DTOs;

public record CreateSessionNoteRequest(
    string? Title,
    string Content
);

public record UpdateSessionNoteRequest(
    string? Title,
    string Content
);

public record SessionNoteResponse(
    Guid NoteId,
    Guid SessionId,
    string? Title,
    string Content,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record DeleteSessionNoteResponse(
    Guid NoteId,
    string Message
);
