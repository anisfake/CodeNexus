using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Application.Features.Goals.Commands.UpdateGoal
{
    public class UpdateGoalCommandValidator : AbstractValidator<UpdateGoalCommand>
    {
        public UpdateGoalCommandValidator()
        {
            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("Title is required.")
                .MaximumLength(100).WithMessage("Title cannot exceed 100 characters.");
            RuleFor(x => x.Description)
                .MaximumLength(100).WithMessage("Description cannot exceed 100 characters.");
            RuleFor(x => x.DurationDays)
                .GreaterThan(0).WithMessage("Duration must be greater than 0.");
        }
    }
}
