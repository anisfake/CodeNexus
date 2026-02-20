using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Goals.DTOs;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Application.Features.Goals.Commands.UpdateGoal
{
    public class UpdateGoalCommandHandler : IRequestHandler<UpdateGoalCommand, Result<GoalDto>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        public UpdateGoalCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }
        public async Task<Result<GoalDto>> Handle(UpdateGoalCommand request, CancellationToken cancellationToken)
        {
            var userId = _currentUserService.GetUserId();

            var goal = _context.Goals.FirstOrDefault(g => g.UserId == userId && g.GoalId == request.GoalId);

            if (goal == null)
            {
                return Result<GoalDto>.Failure("GOAL_NOT_FOUND", "The specified goal was not found.");
            }

            goal.Title = request.Title;
            goal.Description = request.Description;
            goal.DurationDays = request.DurationDays;
            goal.IsCompleted = request.IsCompleted;
            goal.CompletedAt = request.IsCompleted ? request.CompleteAt : null;

            await _context.SaveChangesAsync(cancellationToken);

            return Result<GoalDto>.Success(new GoalDto(
                goal.GoalId,
                goal.Title,
                goal.Description,
                goal.DurationDays,
                goal.IsCompleted,
                goal.CompletedAt,
                goal.CreatedAt
            ));
        }
    }
}
