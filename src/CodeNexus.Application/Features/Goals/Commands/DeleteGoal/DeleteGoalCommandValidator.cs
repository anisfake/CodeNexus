using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Application.Features.Goals.Commands.DeleteGoal
{
    public class DeleteGoalCommandValidator : AbstractValidator<DeleteGoalCommand>
    {
        public DeleteGoalCommandValidator()
        {
            RuleFor(x => x.GoalId)
                .NotEmpty().WithMessage("GoalId is required.");
        }
    }
}
