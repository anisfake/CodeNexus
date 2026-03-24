using FluentValidation;

namespace CodeNexus.Application.Features.LearningPathShares.Commands.AcceptLearningPathShare;

public class AcceptLearningPathShareCommandValidator : AbstractValidator<AcceptLearningPathShareCommand>
{
    public AcceptLearningPathShareCommandValidator()
    {
        RuleFor(x => x.ShareId)
            .NotEmpty()
            .WithErrorCode("SHARE_ID_REQUIRED")
            .WithMessage("ShareId is required.");
    }
}
