using FluentValidation;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Queries.GetStudentLearningPathEditDetail;

public class GetStudentLearningPathEditDetailQueryValidator : AbstractValidator<GetStudentLearningPathEditDetailQuery>
{
    public GetStudentLearningPathEditDetailQueryValidator()
    {
        RuleFor(x => x.PathId)
            .NotEmpty()
            .WithMessage("Path ID is required.")
            .WithErrorCode("PATH_REQUIRED");
    }
}
