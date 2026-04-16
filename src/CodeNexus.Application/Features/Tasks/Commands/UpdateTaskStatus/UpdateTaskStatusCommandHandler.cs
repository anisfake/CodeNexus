using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Tasks.Commands.UpdateTaskStatus;

public class UpdateTaskStatusCommandHandler : IRequestHandler<UpdateTaskStatusCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public UpdateTaskStatusCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(UpdateTaskStatusCommand request, CancellationToken cancellationToken)
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

        task.Status = request.Status;
        task.UpdatedAt = DateTime.UtcNow;

        if (request.Status == TaskStatus_.Completed)
            task.CompletedAt = DateTime.UtcNow;
        else
            task.CompletedAt = null;

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
