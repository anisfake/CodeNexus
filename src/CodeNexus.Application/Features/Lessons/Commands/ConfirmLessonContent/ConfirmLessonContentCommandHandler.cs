using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Lessons.Commands.ConfirmLessonContent;

public class ConfirmLessonContentCommandHandler : IRequestHandler<ConfirmLessonContentCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public ConfirmLessonContentCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(ConfirmLessonContentCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();

        var lesson = await _context.Lessons
            .Include(l => l.Chapter)
                .ThenInclude(c => c.LearningPath)
            .FirstOrDefaultAsync(l => l.LessonId == request.LessonId, cancellationToken);

        if (lesson == null)
            return Result.Failure("LESSON_NOT_FOUND", "Lesson not found");

        if (lesson.Chapter.LearningPath.UserId != userId)
            return Result.Failure("UNAUTHORIZED", "You do not have access to this lesson");

        lesson.Content = request.Content;
        lesson.UpdatedAt = DateTime.Now;

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
