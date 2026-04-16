using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Notes.DTOs;
using CodeNexus.Domain.Entities;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Notes.Commands.CreateSessionNote;

public class CreateSessionNoteCommandHandler : IRequestHandler<CreateSessionNoteCommand, Result<SessionNoteResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public CreateSessionNoteCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<SessionNoteResponse>> Handle(CreateSessionNoteCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();
        var hasAccess = await HasSessionAccess(request.SessionId, userId, cancellationToken);
        if (!hasAccess)
        {
            return Result<SessionNoteResponse>.Failure("SESSION_NOT_FOUND", "Session not found.");
        }

        var content = request.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            return Result<SessionNoteResponse>.Failure("NOTE_CONTENT_REQUIRED", "Note content is required.");
        }

        var note = new Note
        {
            NoteId = NewId.NextGuid(),
            SessionId = request.SessionId,
            Title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim(),
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

        _context.Notes.Add(note);
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
