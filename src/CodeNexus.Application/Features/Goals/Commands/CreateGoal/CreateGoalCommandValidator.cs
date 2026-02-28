using FluentValidation;

namespace CodeNexus.Application.Features.Goals.Commands.CreateGoal
{
    public class CreateGoalCommandValidator : AbstractValidator<CreateGoalCommand>
    {
        public CreateGoalCommandValidator()
        {
            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("Title is required.")
                .MinimumLength(10).WithMessage("Title must be at least 10 characters.")
                .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");
            
            RuleFor(x => x.Description)
                .MaximumLength(500).WithMessage("Description must not exceed 500 characters.");
        }
    }
}
