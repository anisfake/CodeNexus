using FluentValidation;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Commands.GenerateChapterSkeleton;

public class GenerateChapterSkeletonCommandValidator : AbstractValidator<GenerateChapterSkeletonCommand>
{
    public GenerateChapterSkeletonCommandValidator()
    {
        RuleFor(x => x.PathId)
            .NotEmpty()
            .WithMessage("PathId is required");

        RuleFor(x => x.OrderIndex)
            .GreaterThanOrEqualTo(0)
            .WithMessage("OrderIndex must be greater than or equal to 0");
    }
}
