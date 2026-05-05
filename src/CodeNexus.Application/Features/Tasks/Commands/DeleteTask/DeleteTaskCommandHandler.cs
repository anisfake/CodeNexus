using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Tasks.Commands.DeleteTask;

public class DeleteTaskCommandHandler : IRequestHandler<DeleteTaskCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public DeleteTaskCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(DeleteTaskCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();

        var task = await _context.Tasks
            .Include(t => t.Chapter)
                .ThenInclude(c => c.LearningPath)
            .FirstOrDefaultAsync(t => t.TaskId == request.TaskId && !t.IsDeleted, cancellationToken);

        if (task == null)
            return Result.Failure("TASK_NOT_FOUND", "Task not found");

        if (task.Chapter.LearningPath.UserId != userId)
            return Result.Failure("UNAUTHORIZED", "You do not have access to this task");

        task.IsDeleted = true;
        task.DeletedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
