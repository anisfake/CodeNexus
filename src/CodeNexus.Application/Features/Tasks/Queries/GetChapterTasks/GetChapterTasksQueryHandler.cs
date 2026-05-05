using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Tasks.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Tasks.Queries.GetChapterTasks;

public class GetChapterTasksQueryHandler : IRequestHandler<GetChapterTasksQuery, Result<ChapterTasksDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetChapterTasksQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<ChapterTasksDto>> Handle(GetChapterTasksQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();

        var chapter = await _context.Chapters
            .AsNoTracking()
            .Include(c => c.LearningPath)
            .Include(c => c.Tasks.Where(t => !t.IsDeleted))
            .FirstOrDefaultAsync(c => c.ChapterId == request.ChapterId, cancellationToken);

        if (chapter == null)
            return Result<ChapterTasksDto>.Failure("CHAPTER_NOT_FOUND", "Chapter not found");

        if (chapter.LearningPath.UserId != userId)
            return Result<ChapterTasksDto>.Failure("UNAUTHORIZED", "You do not have access to this chapter");

        var dto = new ChapterTasksDto(
            chapter.ChapterId,
            chapter.Title,
            chapter.Tasks.Select(t => new TaskItemDto(
                t.TaskId,
                t.Title,
                t.Description ?? string.Empty,
                t.DueDate,
                t.TaskType,
                t.Priority,
                t.Status
            )).ToList()
        );

        return Result<ChapterTasksDto>.Success(dto);
    }
}
