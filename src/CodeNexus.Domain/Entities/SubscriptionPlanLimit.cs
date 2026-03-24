using CodeNexus.Domain.Enums;

namespace CodeNexus.Domain.Entities;

public class SubscriptionPlanLimit
{
    public Guid SubscriptionPlanLimitId { get; set; }
    public Guid SubscriptionPlanId { get; set; }
    public SubscriptionFeatureKey FeatureKey { get; set; }
    public int? LimitCount { get; set; }
    public UsageWindowType WindowType { get; set; } = UsageWindowType.Monthly;
    public bool IsEnabled { get; set; } = true;

    public virtual SubscriptionPlan SubscriptionPlan { get; set; } = null!;
}

