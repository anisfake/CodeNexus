using FluentValidation;

namespace CodeNexus.Application.Features.LearningPathShares.Commands.SendLearningPathShare;

public class SendLearningPathShareCommandValidator : AbstractValidator<SendLearningPathShareCommand>
{
    public SendLearningPathShareCommandValidator()
    {
        RuleFor(x => x.PathId)
            .NotEmpty()
            .WithErrorCode("PATH_ID_REQUIRED")
            .WithMessage("PathId is required.");

        RuleFor(x => x.StudentId)
            .NotEmpty()
            .WithErrorCode("STUDENT_ID_REQUIRED")
            .WithMessage("StudentId is required.");
    }
}
