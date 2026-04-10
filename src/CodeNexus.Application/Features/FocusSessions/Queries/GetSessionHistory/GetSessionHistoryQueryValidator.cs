using FluentValidation;

namespace CodeNexus.Application.Features.FocusSessions.Queries.GetSessionHistory;

public class GetSessionHistoryQueryValidator : AbstractValidator<GetSessionHistoryQuery>
{
    public GetSessionHistoryQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThan(0)
            .WithMessage("Page number must be greater than 0");

        RuleFor(x => x.PageSize)
            .GreaterThan(0)
            .WithMessage("Page size must be greater than 0")
            .LessThanOrEqualTo(50)
            .WithMessage("Page size cannot exceed 50");

        RuleFor(x => x)
            .Must(x => !x.StartedFrom.HasValue || !x.StartedTo.HasValue || x.StartedFrom <= x.StartedTo)
            .WithMessage("StartedFrom must be less than or equal to StartedTo");
    }
}
