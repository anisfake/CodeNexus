namespace CodeNexus.Domain.Entities;

public class StudentMentorSubscription
{
    public Guid SubscriptionId { get; set; }
    public Guid UserId { get; set; }
    public Guid MentorPackageId { get; set; }
    public Guid? PaymentTransactionId { get; set; }

    /// <summary>Effective limit = package limit + carry-over from previous subscription. -1 = unlimited.</summary>
    public int SharesFromMentorLimit { get; set; }
    public int SharesFromMentorUsed { get; set; }

    /// <summary>-1 = unlimited</summary>
    public int ValidationRequestLimit { get; set; }
    public int ValidationRequestsUsed { get; set; }

    /// <summary>-1 = unlimited (reserved for future use)</summary>
    public int TaskReviewLimit { get; set; }
    public int TaskReviewsUsed { get; set; }

    /// <summary>False when superseded by a newer subscription purchase.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual User User { get; set; } = null!;
    public virtual MentorPackage MentorPackage { get; set; } = null!;
    public virtual PaymentTransaction? PaymentTransaction { get; set; }
}
