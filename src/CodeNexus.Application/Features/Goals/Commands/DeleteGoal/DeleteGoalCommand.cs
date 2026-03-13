using CodeNexus.Application.Common.Models;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Application.Features.Goals.Commands.DeleteGoal
{
    public record DeleteGoalCommand(Guid GoalId) : IRequest<Result<string>>;
}
