namespace CodeNexus.Domain.Entities;

public class MentorPackage
{
    public Guid MentorPackageId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal PriceVnd { get; set; }

    public int SharesFromMentorLimit { get; set; }

    public int ValidationRequestLimit { get; set; }

    public int TaskReviewLimit { get; set; }

    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<StudentMentorSubscription> Subscriptions { get; set; } = new List<StudentMentorSubscription>();
}
