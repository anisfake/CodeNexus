using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Notes.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Notes.Commands.DeleteSessionNote;

public class DeleteSessionNoteCommandHandler : IRequestHandler<DeleteSessionNoteCommand, Result<DeleteSessionNoteResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public DeleteSessionNoteCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<DeleteSessionNoteResponse>> Handle(DeleteSessionNoteCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();
        var hasAccess = await HasSessionAccess(request.SessionId, userId, cancellationToken);
        if (!hasAccess)
        {
            return Result<DeleteSessionNoteResponse>.Failure("SESSION_NOT_FOUND", "Session not found.");
        }

        var note = await _context.Notes
            .FirstOrDefaultAsync(x =>
                x.NoteId == request.NoteId
                && x.SessionId == request.SessionId
                && !x.IsDeleted,
                cancellationToken);

        if (note == null)
        {
            return Result<DeleteSessionNoteResponse>.Failure("NOTE_NOT_FOUND", "Note not found.");
        }

        note.IsDeleted = true;
        note.DeletedAt = DateTime.UtcNow;
        note.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return Result<DeleteSessionNoteResponse>.Success(new DeleteSessionNoteResponse(
            request.NoteId,
            "Note deleted successfully."));
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
