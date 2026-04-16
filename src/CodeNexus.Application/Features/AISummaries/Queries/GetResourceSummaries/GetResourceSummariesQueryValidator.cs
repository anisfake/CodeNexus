using FluentValidation;

namespace CodeNexus.Application.Features.AISummaries.Queries.GetResourceSummaries;

public class GetResourceSummariesQueryValidator : AbstractValidator<GetResourceSummariesQuery>
{
    public GetResourceSummariesQueryValidator()
    {
        RuleFor(x => x.ResourceId)
            .NotEmpty()
            .WithMessage("Resource ID is required.")
            .WithErrorCode("RESOURCE_ID_REQUIRED");
    }
}
