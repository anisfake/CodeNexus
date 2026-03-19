using FluentValidation;

namespace CodeNexus.Application.Features.LearningPathShares.Queries.GetSentLearningPathShares;

public class GetSentLearningPathSharesQueryValidator : AbstractValidator<GetSentLearningPathSharesQuery>
{
    public GetSentLearningPathSharesQueryValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum()
            .When(x => x.Status.HasValue)
            .WithErrorCode("INVALID_STATUS")
            .WithMessage("Invalid learning path share status.");
    }
}
