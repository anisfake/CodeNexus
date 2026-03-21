using FluentValidation;

namespace CodeNexus.Application.Features.Subscriptions.Commands.DeleteSubscriptionPlan;

public class DeleteSubscriptionPlanCommandValidator : AbstractValidator<DeleteSubscriptionPlanCommand>
{
    public DeleteSubscriptionPlanCommandValidator()
    {
        RuleFor(x => x.SubscriptionPlanId).NotEmpty();
    }
}
