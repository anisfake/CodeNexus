using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Notes.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Notes.Commands.UpdateSessionNote;

public class UpdateSessionNoteCommandHandler : IRequestHandler<UpdateSessionNoteCommand, Result<SessionNoteResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public UpdateSessionNoteCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<SessionNoteResponse>> Handle(UpdateSessionNoteCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();
        var hasAccess = await HasSessionAccess(request.SessionId, userId, cancellationToken);
        if (!hasAccess)
        {
            return Result<SessionNoteResponse>.Failure("SESSION_NOT_FOUND", "Session not found.");
        }

        var note = await _context.Notes
            .FirstOrDefaultAsync(x =>
                x.NoteId == request.NoteId
                && x.SessionId == request.SessionId
                && !x.IsDeleted,
                cancellationToken);

        if (note == null)
        {
            return Result<SessionNoteResponse>.Failure("NOTE_NOT_FOUND", "Note not found.");
        }

        var content = request.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            return Result<SessionNoteResponse>.Failure("NOTE_CONTENT_REQUIRED", "Note content is required.");
        }

        note.Title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim();
        note.Content = content;
        note.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return Result<SessionNoteResponse>.Success(new SessionNoteResponse(
            note.NoteId,
            request.SessionId,
            note.Title,
            note.Content ?? string.Empty,
            note.CreatedAt,
            note.UpdatedAt));
    }

    private async Task<bool> HasSessionAccess(Guid sessionId, Guid userId, CancellationToken cancellationToken)
    {
        return await _context.FocusSessions
            .Join(_context.Tasks,
                session => session.TaskId,
                task => task.TaskId,
                (session, task) => new { session, task })
            .Join(_context.LearningPaths,
                sessionTask => sessionTask.task.PathId,
                path => path.PathId,
                (sessionTask, path) => new { sessionTask.session, path.UserId })
            .AnyAsync(x => x.session.SessionId == sessionId && x.UserId == userId, cancellationToken);
    }
}
