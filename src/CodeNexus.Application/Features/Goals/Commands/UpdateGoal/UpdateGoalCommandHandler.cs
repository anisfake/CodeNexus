using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Goals.DTOs;
using CodeNexus.Domain.Entities;
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

            var subject = await _context.Subjects
                .FirstOrDefaultAsync(s => s.SubjectId == request.SubjectId && !s.IsDeleted, cancellationToken);

            if (subject == null)
            {
                return Result<GoalDto>.Failure("SUBJECT_NOT_FOUND", "Subject not found");
            }

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

            var normalizedTitle = request.Title.Trim();
            var normalizedDescription = request.Description?.Trim();

            var currentSubjectIds = await _context.SubjectGoals
                .Where(sg => sg.GoalId == goal.GoalId)
                .Select(sg => sg.SubjectId)
                .ToListAsync(cancellationToken);

            var isSubjectChanged = currentSubjectIds.Count != 1 || currentSubjectIds[0] != request.SubjectId;
            var isContentChanged = goal.Title != normalizedTitle || goal.Description != normalizedDescription;

            if (isContentChanged || isSubjectChanged)
            {
                var isValid = await _goalValidationService.IsRelatedToProgrammingAsync(normalizedTitle, cancellationToken);
                if (!isValid)
                {
                    return Result<GoalDto>.Failure(
                        "INVALID_GOAL",
                        "Goals must be related to programming or software development");
                }

                var isRelevantToSubject = await _goalValidationService.IsGoalRelevantToSubjectAsync(
                    normalizedTitle,
                    normalizedDescription,
                    subject.Name,
                    subject.Description,
                    cancellationToken);

                if (!isRelevantToSubject)
                {
                    return Result<GoalDto>.Failure(
                        "GOAL_SUBJECT_MISMATCH",
                        "Goal is not relevant to the selected subject.");
                }
            }

            goal.Title = normalizedTitle;
            goal.Description = normalizedDescription;
            goal.IsActive = request.IsActive;
            goal.Duration = request.Duration;
            goal.UpdatedAt = DateTime.UtcNow;

            if (isSubjectChanged)
            {
                var currentMappings = await _context.SubjectGoals
                    .Where(sg => sg.GoalId == goal.GoalId)
                    .ToListAsync(cancellationToken);

                if (currentMappings.Count > 0)
                {
                    _context.SubjectGoals.RemoveRange(currentMappings);
                }

                await _context.SubjectGoals.AddAsync(new SubjectGoal
                {
                    SubjectId = subject.SubjectId,
                    GoalId = goal.GoalId
                }, cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);

            return Result<GoalDto>.Success(new GoalDto(
                goal.GoalId,
                goal.Title,
                goal.Description,
                goal.IsSystemDefined,
                goal.IsActive,
                goal.Duration,
                goal.DurationInDays,
                goal.CreatedAt
            ));
        }
    }
}
