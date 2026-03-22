using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Lessons.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Lessons.Queries.GetLessonReadStatus;

public class GetLessonReadStatusQueryHandler : IRequestHandler<GetLessonReadStatusQuery, Result<LessonReadStatusDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetLessonReadStatusQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<LessonReadStatusDto>> Handle(GetLessonReadStatusQuery request, CancellationToken cancellationToken)
    {
        Guid userId;
        try
        {
            userId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<LessonReadStatusDto>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        var lesson = await _context.Lessons
            .AsNoTracking()
            .Include(l => l.Chapter)
            .ThenInclude(c => c.LearningPath)
            .FirstOrDefaultAsync(l => l.LessonId == request.LessonId && !l.IsDeleted && !l.Chapter.IsDeleted, cancellationToken);

        if (lesson == null)
        {
            return Result<LessonReadStatusDto>.Failure("LESSON_NOT_FOUND", "Lesson not found");
        }

        if (lesson.Chapter.LearningPath.UserId != userId)
        {
            return Result<LessonReadStatusDto>.Failure("ACCESS_DENIED", "You do not have access to this lesson");
        }

        var progress = await _context.LearnProgresses
            .AsNoTracking()
            .Where(x => x.LessonId == request.LessonId && x.UserId == userId)
            .Select(x => new { x.IsLessonContentRead, x.CompletedAt })
            .FirstOrDefaultAsync(cancellationToken);

        if (progress == null)
        {
            return Result<LessonReadStatusDto>.Success(new LessonReadStatusDto(request.LessonId, false, null));
        }

        DateTime? readAt = progress.IsLessonContentRead ? progress.CompletedAt : null;
        return Result<LessonReadStatusDto>.Success(new LessonReadStatusDto(request.LessonId, progress.IsLessonContentRead, readAt));
    }
}
