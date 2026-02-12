using FluentValidation;

namespace CodeNexus.Application.Features.LearningPaths.Commands.GenerateLearningPathSkeleton;

public class GenerateLearningPathSkeletonCommandValidator : AbstractValidator<GenerateLearningPathSkeletonCommand>
{
    public GenerateLearningPathSkeletonCommandValidator()
    {
        RuleFor(x => x.SubjectId)
            .NotEmpty()
            .WithMessage("Subject ID is required");

        RuleFor(x => x.GoalId)
            .NotEmpty()
            .WithMessage("Goal ID is required");
    }
}
