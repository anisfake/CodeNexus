using FluentValidation;

namespace CodeNexus.Application.Features.Tasks.Commands.GenerateSingleTask;

public class GenerateSingleTaskCommandValidator : AbstractValidator<GenerateSingleTaskCommand>
{
    public GenerateSingleTaskCommandValidator()
    {
        RuleFor(x => x.ChapterId)
            .NotEmpty()
            .WithMessage("Chapter ID is required")
            .WithErrorCode("CHAPTER_ID_REQUIRED");

        RuleFor(x => x.TaskType)
            .IsInEnum()
            .WithMessage("Task type is invalid")
            .WithErrorCode("TASK_TYPE_INVALID");

        RuleFor(x => x.Title)
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.Title))
            .WithMessage("Task title must not exceed 200 characters")
            .WithErrorCode("TASK_TITLE_TOO_LONG");
    }
}
