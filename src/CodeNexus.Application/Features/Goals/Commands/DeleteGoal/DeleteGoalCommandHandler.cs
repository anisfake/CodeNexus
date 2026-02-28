using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Goals.Commands.DeleteGoal
{
    public class DeleteGoalCommandHandler : IRequestHandler<DeleteGoalCommand, Result<string>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public DeleteGoalCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<Result<string>> Handle(DeleteGoalCommand request, CancellationToken cancellationToken)
        {
            var userId = _currentUserService.GetUserId();

            var goal = await _context.Goals
                .FirstOrDefaultAsync(g =>
                    g.GoalId == request.GoalId
                    && g.CreatedByUserId == userId
                    && !g.IsDeleted,
                    cancellationToken);

            if (goal == null)
            {
                return Result<string>.Failure("GOAL_NOT_FOUND", "The specified goal was not found.");
            }

            if (goal.IsSystemDefined)
            {
                return Result<string>.Failure("CANNOT_DELETE_SYSTEM_GOAL", "Cannot delete system goal");
            }

            var goalInLearningPath = await _context.LearningPaths
                .AnyAsync(lp => lp.GoalId == request.GoalId, cancellationToken);

            if (goalInLearningPath)
            {
                return Result<string>.Failure(
                    "GOAL_IN_USE",
                    "The specified goal is currently in use in a learning path and cannot be deleted.");
            }

            goal.IsDeleted = true;
            goal.DeletedAt = DateTime.Now;

            await _context.SaveChangesAsync(cancellationToken);
            return Result<string>.Success("Delete goal successful!");
        }
    }
}
