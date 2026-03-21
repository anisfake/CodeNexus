using FluentValidation;

namespace CodeNexus.Application.Features.LearningPaths.Queries.GetLearningPathProgress;

public class GetLearningPathProgressQueryValidator : AbstractValidator<GetLearningPathProgressQuery>
{
    public GetLearningPathProgressQueryValidator()
    {
        RuleFor(x => x.PathId)
            .NotEmpty().WithMessage("PathId is required.");
    }
}