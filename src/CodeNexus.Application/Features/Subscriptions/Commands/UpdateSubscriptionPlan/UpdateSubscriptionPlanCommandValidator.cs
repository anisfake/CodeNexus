using FluentValidation;

namespace CodeNexus.Application.Features.Subscriptions.Commands.UpdateSubscriptionPlan;

public class UpdateSubscriptionPlanCommandValidator : AbstractValidator<UpdateSubscriptionPlanCommand>
{
    public UpdateSubscriptionPlanCommandValidator()
    {
        RuleFor(x => x.SubscriptionPlanId).NotEmpty();
        RuleFor(x => x.PlanType).IsInEnum();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.PriceVnd).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DurationDays).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);

        RuleForEach(x => x.Limits).ChildRules(limit =>
        {
            limit.RuleFor(x => x.FeatureKey).IsInEnum();
            limit.RuleFor(x => x.WindowType).IsInEnum();
            limit.RuleFor(x => x.LimitCount)
                .GreaterThanOrEqualTo(0)
                .When(x => x.LimitCount.HasValue);
        });

        RuleFor(x => x.Limits)
            .Must(limits => limits == null || limits.Select(x => x.FeatureKey).Distinct().Count() == limits.Count)
            .WithMessage("Duplicate feature limits are not allowed.");
    }
}
