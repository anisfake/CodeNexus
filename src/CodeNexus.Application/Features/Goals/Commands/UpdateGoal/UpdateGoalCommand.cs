using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Goals.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;

namespace CodeNexus.Application.Features.Goals.Commands.UpdateGoal
{
    public record UpdateGoalCommand(
        Guid GoalId,
        Guid SubjectId,
        string Title,
        string? Description,
        GoalDuration Duration) : IRequest<Result<GoalDto>>;
}
