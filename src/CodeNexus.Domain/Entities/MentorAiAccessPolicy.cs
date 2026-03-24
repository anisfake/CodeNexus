namespace CodeNexus.Domain.Entities;

public class MentorAiAccessPolicy
{
    public Guid MentorAiAccessPolicyId { get; set; }
    public int MentorPaidRequestsMonthlyLimit { get; set; }
    public int MentorDowngradeNotifyCooldownHours { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

