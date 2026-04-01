using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Helpers;
using CodeNexus.Application.Common.Models;
using CodeNexus.Domain.Entities;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Lessons.Commands.MarkLessonContentRead;

public class MarkLessonContentReadCommandHandler : IRequestHandler<MarkLessonContentReadCommand, Result<string>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public MarkLessonContentReadCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<string>> Handle(MarkLessonContentReadCommand request, CancellationToken cancellationToken)
    {
        Guid userId;
        try
        {
            userId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<string>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        var lesson = await _context.Lessons
            .AsNoTracking()
            .Include(l => l.Chapter)
            .ThenInclude(c => c.LearningPath)
            .FirstOrDefaultAsync(l => l.LessonId == request.LessonId && !l.IsDeleted && !l.Chapter.IsDeleted, cancellationToken);

        if (lesson == null)
        {
            return Result<string>.Failure("LESSON_NOT_FOUND", "Lesson not found");
        }

        if (lesson.Chapter.LearningPath.UserId != userId)
        {
            return Result<string>.Failure("ACCESS_DENIED", "Access denied.");
        }

        var progress = await _context.LearnProgresses
            .FirstOrDefaultAsync(x => x.LessonId == request.LessonId && x.UserId == userId, cancellationToken);

        if (progress == null)
        {
            progress = new LearnProgress
            {
                ProgressId = NewId.NextGuid(),
                LessonId = request.LessonId,
                UserId = userId,
                IsLessonContentRead = true,
                CompletedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _context.LearnProgresses.Add(progress);
        }
        else
        {
            progress.IsLessonContentRead = true;
            progress.CompletedAt = DateTime.UtcNow;
            progress.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        var hasGoalProgressChanges = await UserGoalProgressSyncHelper.SyncForLearningPathAsync(
            _context,
            lesson.Chapter.PathId,
            userId,
            cancellationToken);

        if (hasGoalProgressChanges)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Result<string>.Success("Lesson content marked as read");
    }
}
