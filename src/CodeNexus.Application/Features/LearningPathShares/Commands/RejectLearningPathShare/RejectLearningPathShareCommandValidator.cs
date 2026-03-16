using FluentValidation;

namespace CodeNexus.Application.Features.LearningPathShares.Commands.RejectLearningPathShare;

public class RejectLearningPathShareCommandValidator : AbstractValidator<RejectLearningPathShareCommand>
{
    public RejectLearningPathShareCommandValidator()
    {
        RuleFor(x => x.ShareId)
            .NotEmpty()
            .WithErrorCode("SHARE_ID_REQUIRED")
            .WithMessage("ShareId is required.");
    }
}
