using FluentValidation;

namespace CodeNexus.Application.Features.AISummaries.Commands.DeleteResourceSummary;

public class DeleteResourceSummaryCommandValidator : AbstractValidator<DeleteResourceSummaryCommand>
{
    public DeleteResourceSummaryCommandValidator()
    {
        RuleFor(x => x.SummaryId)
            .NotEmpty()
            .WithMessage("Summary ID is required.")
            .WithErrorCode("SUMMARY_ID_REQUIRED");
    }
}
