using FluentValidation;

namespace CodeNexus.Application.Features.MentorValidationRequests.Commands.RespondToValidationRequest;

public class RespondToValidationRequestCommandValidator : AbstractValidator<RespondToValidationRequestCommand>
{
    public RespondToValidationRequestCommandValidator()
    {
        RuleFor(x => x.ValidationRequestId)
            .NotEmpty()
            .WithErrorCode("INVALID_VALIDATION_REQUEST_ID")
            .WithMessage("Validation request ID is required.");

        RuleFor(x => x.Feedback)
            .MaximumLength(2000)
            .WithErrorCode("FEEDBACK_TOO_LONG")
            .WithMessage("Feedback must not exceed 2000 characters.")
            .When(x => x.Feedback != null);
    }
}
