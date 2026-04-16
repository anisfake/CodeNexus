using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Notes.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Notes.Queries.GetSessionNotes;

public class GetSessionNotesQueryHandler : IRequestHandler<GetSessionNotesQuery, Result<List<SessionNoteResponse>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetSessionNotesQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<List<SessionNoteResponse>>> Handle(GetSessionNotesQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();
        var hasAccess = await HasSessionAccess(request.SessionId, userId, cancellationToken);
        if (!hasAccess)
        {
            return Result<List<SessionNoteResponse>>.Failure("SESSION_NOT_FOUND", "Session not found.");
        }

        var notes = await _context.Notes
            .AsNoTracking()
            .Where(x => x.SessionId == request.SessionId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new SessionNoteResponse(
                x.NoteId,
                request.SessionId,
                x.Title,
                x.Content ?? string.Empty,
                x.CreatedAt,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Result<List<SessionNoteResponse>>.Success(notes);
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
