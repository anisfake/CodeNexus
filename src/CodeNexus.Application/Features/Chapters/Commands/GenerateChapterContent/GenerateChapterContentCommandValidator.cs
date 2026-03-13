using FluentValidation;

namespace CodeNexus.Application.Features.Chapters.Commands.GenerateChapterContent;

public class GenerateChapterContentCommandValidator : AbstractValidator<GenerateChapterContentCommand>
{
    public GenerateChapterContentCommandValidator()
    {
        RuleFor(x => x.ChapterId)
            .NotEmpty()
            .WithMessage("Chapter ID is required")
            .WithErrorCode("CHAPTER_ID_REQUIRED");
    }
}
