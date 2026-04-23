namespace CodeNexus.Domain.Entities;

public class MentorPackage
{
    public Guid MentorPackageId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal PriceVnd { get; set; }

    /// <summary>-1 = unlimited</summary>
    public int SharesFromMentorLimit { get; set; }

    /// <summary>-1 = unlimited</summary>
    public int ValidationRequestLimit { get; set; }

    /// <summary>-1 = unlimited (reserved for future use)</summary>
    public int TaskReviewLimit { get; set; }

    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<StudentMentorSubscription> Subscriptions { get; set; } = new List<StudentMentorSubscription>();
}
