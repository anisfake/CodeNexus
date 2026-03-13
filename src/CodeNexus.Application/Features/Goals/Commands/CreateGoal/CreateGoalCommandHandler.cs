using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Goals.DTOs;
using CodeNexus.Domain.Entities;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Goals.Commands.CreateGoal;

public class CreateGoalCommandHandler : IRequestHandler<CreateGoalCommand, Result<CreateGoalResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IGoalValidationService _goalValidationService;

    public CreateGoalCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IGoalValidationService goalValidationService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _goalValidationService = goalValidationService;
    }

    public async Task<Result<CreateGoalResponseDto>> Handle(CreateGoalCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();

        var isValid = await _goalValidationService.IsRelatedToProgrammingAsync(request.Title, cancellationToken);
        if (!isValid)
        {
            return Result<CreateGoalResponseDto>.Failure(
                "INVALID_GOAL",
                "Goal must be related to programming or software development.");
        }

        var existingGoal = await _context.Goals
            .FirstOrDefaultAsync(g =>
                g.CreatedByUserId == userId
                && g.Title.ToLower() == request.Title.ToLower()
                && !g.IsDeleted,
                cancellationToken);

        if (existingGoal != null)
        {
            return Result<CreateGoalResponseDto>.Failure(
                "GOAL_ALREADY_EXISTS",
                "You already have this goal");
        }

        var goal = new Domain.Entities.Goals
        {
            GoalId = NewId.NextGuid(),
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            IsSystemDefined = false,
            CreatedByUserId = userId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            await _context.Goals.AddAsync(goal, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            return Result<CreateGoalResponseDto>.Failure(
                "CREATE_GOAL_FAILED",
                $"An error occurred while creating the goal: {ex.Message}");
        }

        var responseDto = new CreateGoalResponseDto(
            goal.GoalId,
            goal.Title,
            goal.Description,
            goal.IsSystemDefined
        );

        return Result<CreateGoalResponseDto>.Success(responseDto);
    }
}
