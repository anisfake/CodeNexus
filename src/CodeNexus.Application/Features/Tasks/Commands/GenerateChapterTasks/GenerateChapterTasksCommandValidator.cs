using FluentValidation;

namespace CodeNexus.Application.Features.Tasks.Commands.GenerateChapterTasks;

public class GenerateChapterTasksCommandValidator : AbstractValidator<GenerateChapterTasksCommand>
{
    public GenerateChapterTasksCommandValidator()
    {
        RuleFor(x => x.ChapterId)
            .NotEmpty()
            .WithMessage("Chapter ID is required")
            .WithErrorCode("CHAPTER_ID_REQUIRED");
    }
}
