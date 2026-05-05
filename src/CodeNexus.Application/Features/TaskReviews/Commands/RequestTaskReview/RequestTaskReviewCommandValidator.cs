using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.TaskReviews.DTOs;
using FluentValidation;

namespace CodeNexus.Application.Features.TaskReviews.Commands.RequestTaskReview;

public class RequestTaskReviewCommandValidator : AbstractValidator<RequestTaskReviewCommand>
{
    public RequestTaskReviewCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithErrorCode("SESSION_ID_REQUIRED")
            .WithMessage("Session ID is required.");

        RuleFor(x => x.MentorId)
            .NotEmpty()
            .WithErrorCode("MENTOR_ID_REQUIRED")
            .WithMessage("Mentor ID is required.");

        RuleFor(x => x.StudentRequestNote)
            .MaximumLength(500)
            .When(x => x.StudentRequestNote != null)
            .WithErrorCode("REQUEST_NOTE_TOO_LONG")
            .WithMessage("Student request note must not exceed 500 characters.");
    }
}
