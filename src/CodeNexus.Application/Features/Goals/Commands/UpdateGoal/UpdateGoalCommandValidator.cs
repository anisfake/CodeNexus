using FluentValidation;

namespace CodeNexus.Application.Features.Goals.Commands.UpdateGoal
{
    public class UpdateGoalCommandValidator : AbstractValidator<UpdateGoalCommand>
    {
        public UpdateGoalCommandValidator()
        {
            RuleFor(x => x.GoalId)
                .NotEmpty().WithMessage("GoalId is required.");

            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("Title is required.")
                .MinimumLength(10).WithMessage("Title must be at least 10 characters.")
                .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");

            RuleFor(x => x.Description)
                .MaximumLength(500).WithMessage("Description must not exceed 500 characters.");
        }
    }
}
