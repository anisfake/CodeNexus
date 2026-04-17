using FluentValidation;

namespace CodeNexus.Application.Features.Tasks.Commands.UpdateTaskStatus;

public class UpdateTaskStatusCommandValidator : AbstractValidator<UpdateTaskStatusCommand>
{
    public UpdateTaskStatusCommandValidator()
    {
        RuleFor(x => x.TaskId)
            .NotEmpty()
            .WithMessage("Task ID is required")
            .WithErrorCode("TASK_ID_REQUIRED");
    }
}
