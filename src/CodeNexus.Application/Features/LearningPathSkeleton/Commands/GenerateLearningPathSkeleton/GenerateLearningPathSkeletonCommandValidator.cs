using FluentValidation;
using CodeNexus.Domain.Enums;
using System.Linq;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Commands.GenerateLearningPathSkeleton;

public class GenerateLearningPathSkeletonCommandValidator : AbstractValidator<GenerateLearningPathSkeletonCommand>
{
    public GenerateLearningPathSkeletonCommandValidator()
    {
        RuleFor(x => x.SubjectId)
            .NotEmpty()
            .WithMessage("Subject ID is required");

        RuleFor(x => x.Goals)
            .NotNull()
            .WithMessage("Goals are required")
            .Must(g => g != null && g.Count >= 1 && g.Count <= 2)
            .WithMessage("Please select between 1 and 2 goals");

        RuleForEach(x => x.Goals)
            .ChildRules(goal =>
            {
                goal.RuleFor(g => g.GoalId)
                    .NotEmpty()
                    .WithMessage("Goal ID is required");

                goal.RuleFor(g => g.Weight)
                    .GreaterThan(0)
                    .WithMessage("Goal weight must be greater than 0");
            });

        RuleFor(x => x.Goals)
            .Must(goals => goals == null || goals.Select(g => g.GoalId).Distinct().Count() == goals.Count)
            .WithMessage("Duplicate goals are not allowed");

        RuleFor(x => x.ComplexityLevel)
            .IsInEnum()
            .WithMessage("Complexity level must be Beginner, Intermediate, or Advanced");

        RuleFor(x => x.LanguageSelection)
            .IsInEnum()
            .WithMessage("Language selection must be Vietnamese or English");
    }
}

