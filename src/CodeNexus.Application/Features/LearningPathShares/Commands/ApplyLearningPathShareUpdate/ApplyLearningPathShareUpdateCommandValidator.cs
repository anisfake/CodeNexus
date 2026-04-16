using FluentValidation;

namespace CodeNexus.Application.Features.LearningPathShares.Commands.ApplyLearningPathShareUpdate;

public class ApplyLearningPathShareUpdateCommandValidator : AbstractValidator<ApplyLearningPathShareUpdateCommand>
{
    public ApplyLearningPathShareUpdateCommandValidator()
    {
        RuleFor(x => x.ShareId)
            .NotEmpty()
            .WithMessage("ShareId is required.")
            .WithErrorCode("SHARE_ID_REQUIRED");
    }
}
