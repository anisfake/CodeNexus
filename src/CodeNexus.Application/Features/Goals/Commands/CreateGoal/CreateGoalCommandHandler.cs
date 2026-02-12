using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Goals.DTOs;
using CodeNexus.Domain.Entities;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace CodeNexus.Application.Features.Goals.Commands.CreateGoal;

public class CreateGoalCommandHandler : IRequestHandler<CreateGoalCommand, Result<CreateGoalResponeDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    public CreateGoalCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }
    public async Task<Result<CreateGoalResponeDto>> Handle(CreateGoalCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();

        var goal = new Domain.Entities.Goals
        {
            Title = request.Title,
            Description = request.Description,
            UserId = userId,
            CreatedAt = DateTime.Now,
            DurationDays = request.DurationsDay,
            IsCompleted = false
        };

        try
        {
            await _context.Goals.AddAsync(goal);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            return Result<CreateGoalResponeDto>.Failure("CREATE_GOAL_FAILED", $"An error occurred while creating the goal: {ex.Message}");
        }

        var responseDto = new CreateGoalResponeDto(
            goal.Title,
            goal.Description,
            goal.DurationDays
        );

        return Result<CreateGoalResponeDto>.Success(responseDto);
    }
}

