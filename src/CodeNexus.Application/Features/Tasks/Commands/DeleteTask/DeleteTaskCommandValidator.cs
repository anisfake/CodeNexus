using FluentValidation;

namespace CodeNexus.Application.Features.Tasks.Commands.DeleteTask;

public class DeleteTaskCommandValidator : AbstractValidator<DeleteTaskCommand>
{
    public DeleteTaskCommandValidator()
    {
        RuleFor(x => x.TaskId)
            .NotEmpty()
            .WithMessage("Task ID is required")
            .WithErrorCode("TASK_ID_REQUIRED");
    }
}
