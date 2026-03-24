using FluentValidation;

namespace CodeNexus.Application.Features.LearningPathShares.Queries.GetLearningPathSharePreview;

public class GetLearningPathSharePreviewQueryValidator : AbstractValidator<GetLearningPathSharePreviewQuery>
{
    public GetLearningPathSharePreviewQueryValidator()
    {
        RuleFor(x => x.ShareId)
            .NotEmpty()
            .WithErrorCode("SHARE_ID_REQUIRED")
            .WithMessage("ShareId is required.");
    }
}
