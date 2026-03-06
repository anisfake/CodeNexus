using FluentValidation;

namespace CodeNexus.Application.Features.AISummaries.Commands.GenerateResourceSummary;

public class GenerateResourceSummaryCommandValidator : AbstractValidator<GenerateResourceSummaryCommand>
{
    public GenerateResourceSummaryCommandValidator()
    {
        RuleFor(x => x.ResourceId)
            .NotEmpty()
            .WithMessage("Resource ID is required.")
            .WithErrorCode("RESOURCE_ID_REQUIRED");

        RuleFor(x => x.StartPage)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Start page must be at least 1.")
            .WithErrorCode("INVALID_START_PAGE");

        RuleFor(x => x.EndPage)
            .GreaterThanOrEqualTo(x => x.StartPage)
            .WithMessage("End page must be greater than or equal to start page.")
            .WithErrorCode("INVALID_END_PAGE");

        RuleFor(x => x)
            .Must(x => x.EndPage - x.StartPage + 1 <= 5)
            .WithMessage("You can summarize a maximum of 5 pages at a time.")
            .WithErrorCode("MAX_PAGES_EXCEEDED");
    }
}
