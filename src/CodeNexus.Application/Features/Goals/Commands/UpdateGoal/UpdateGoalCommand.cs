using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Goals.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Goals.Commands.UpdateGoal
{
    public record UpdateGoalCommand(
        Guid GoalId,
        string Title,
        string? Description,
        bool IsActive) : IRequest<Result<GoalDto>>;
}
