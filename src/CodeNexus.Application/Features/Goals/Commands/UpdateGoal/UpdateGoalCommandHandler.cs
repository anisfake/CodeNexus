using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Goals.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Goals.Commands.UpdateGoal
{
    public class UpdateGoalCommandHandler : IRequestHandler<UpdateGoalCommand, Result<GoalDto>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IGoalValidationService _goalValidationService;

        public UpdateGoalCommandHandler(
            IApplicationDbContext context,
            ICurrentUserService currentUserService,
            IGoalValidationService goalValidationService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _goalValidationService = goalValidationService;
        }

        public async Task<Result<GoalDto>> Handle(UpdateGoalCommand request, CancellationToken cancellationToken)
        {
            var userId = _currentUserService.GetUserId();

            var goal = await _context.Goals
                .FirstOrDefaultAsync(g =>
                    g.CreatedByUserId == userId
                    && g.GoalId == request.GoalId
                    && !g.IsDeleted,
                    cancellationToken);

            if (goal == null)
            {
                return Result<GoalDto>.Failure("GOAL_NOT_FOUND", "Goal not found");
            }

            if (goal.IsSystemDefined)
            {
                return Result<GoalDto>.Failure("CANNOT_UPDATE_SYSTEM_GOAL", "Cannot edit system goal");
            }

            if (goal.Title != request.Title.Trim())
            {
                var isValid = await _goalValidationService.IsRelatedToProgrammingAsync(request.Title, cancellationToken);
                if (!isValid)
                {
                    return Result<GoalDto>.Failure(
                        "INVALID_GOAL",
                        "Goals must be related to programming or software development");
                }
            }

            goal.Title = request.Title.Trim();
            goal.Description = request.Description?.Trim();
            goal.IsActive = request.IsActive;
            goal.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync(cancellationToken);

            return Result<GoalDto>.Success(new GoalDto(
                goal.GoalId,
                goal.Title,
                goal.Description,
                goal.IsSystemDefined,
                goal.IsActive,
                goal.CreatedAt
            ));
        }
    }
}
