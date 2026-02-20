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
    public record UpdateGoalCommand(
        Guid GoalId,
        string Title,
        string? Description,
        DateTime? CompleteAt,
        int DurationDays,
        bool IsCompleted) : IRequest<Result<GoalDto>>;
}
