using CodeNexus.Domain.Enums;

namespace CodeNexus.Domain.Entities;

public class FeatureUsageLog
{
    public Guid FeatureUsageLogId { get; set; }
    public Guid UserId { get; set; }
    public SubscriptionFeatureKey FeatureKey { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual User User { get; set; } = null!;
}

