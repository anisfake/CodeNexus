using CodeNexus.Domain.Enums;

namespace CodeNexus.Domain.Entities;

public class SubscriptionPlan
{
    public Guid SubscriptionPlanId { get; set; }
    public SubscriptionPlanType PlanType { get; set; } = SubscriptionPlanType.Free;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal PriceVnd { get; set; }
    public int DurationDays { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}
