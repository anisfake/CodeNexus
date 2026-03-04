using FluentValidation;
using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Commands.GenerateLearningPathSkeleton;

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

        RuleFor(x => x.ComplexityLevel)
            .IsInEnum()
            .WithMessage("Complexity level must be Beginner, Intermediate, or Advanced");

        RuleFor(x => x.LanguageSelection)
            .IsInEnum()
            .WithMessage("Language selection must be Vietnamese or English");
    }
}
