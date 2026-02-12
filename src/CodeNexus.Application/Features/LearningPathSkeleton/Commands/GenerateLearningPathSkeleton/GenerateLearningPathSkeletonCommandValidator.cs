using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
