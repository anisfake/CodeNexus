using CodeNexus.Domain.Enums;

namespace CodeNexus.Domain.Entities;

public class TaskReview
{
    public Guid ReviewId { get; set; }

    public Guid SessionId { get; set; }
    public virtual FocusSession Session { get; set; } = null!;

    public Guid TaskId { get; set; }
    public virtual Tasks Task { get; set; } = null!;

    public Guid StudentId { get; set; }
    public virtual User Student { get; set; } = null!;

    public Guid MentorId { get; set; }
    public virtual User Mentor { get; set; } = null!;

    public Guid SubscriptionId { get; set; }
    public virtual StudentMentorSubscription Subscription { get; set; } = null!;

    public int? Score { get; set; }
    public string? Feedback { get; set; }
    public string? Suggestions { get; set; }
    public string? StudentRequestNote { get; set; }

    public TaskReviewStatus Status { get; set; } = TaskReviewStatus.Pending;

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
}
