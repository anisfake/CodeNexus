namespace CodeNexus.Domain.Entities;

public class StudentMentorSubscription
{
    public Guid SubscriptionId { get; set; }
    public Guid UserId { get; set; }
    public Guid MentorPackageId { get; set; }
    public Guid? PaymentTransactionId { get; set; }

    public int SharesFromMentorLimit { get; set; }
    public int SharesFromMentorUsed { get; set; }

    public int ValidationRequestLimit { get; set; }
    public int ValidationRequestsUsed { get; set; }

    public int TaskReviewLimit { get; set; }
    public int TaskReviewsUsed { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual User User { get; set; } = null!;
    public virtual MentorPackage MentorPackage { get; set; } = null!;
    public virtual PaymentTransaction? PaymentTransaction { get; set; }
}
