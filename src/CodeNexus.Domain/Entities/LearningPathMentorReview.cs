using CodeNexus.Domain.Enums;

namespace CodeNexus.Domain.Entities;

public class LearningPathMentorReview
{
    public Guid ReviewId { get; set; }
    public Guid PathId { get; set; }
    public virtual LearningPath LearningPath { get; set; } = null!;

    public Guid? RevisedPathId { get; set; }
    public virtual LearningPath? RevisedLearningPath { get; set; }

    public Guid MentorId { get; set; }
    public virtual User Mentor { get; set; } = null!;

    public Guid StudentId { get; set; }
    public virtual User Student { get; set; } = null!;

    public int Score { get; set; }
    public string Feedback { get; set; } = string.Empty;
    public string? Suggestions { get; set; }
    public string? ChangeSummary { get; set; }
    public string? ChangeReason { get; set; }
    public string? StudentRequestNote { get; set; }
    public int RejectionCount { get; set; }
    public int MaxRejections { get; set; } = 3;
    public LearningPathMentorReviewDecisionStatus DecisionStatus { get; set; } = LearningPathMentorReviewDecisionStatus.Pending;
    public string? StudentDecisionNote { get; set; }
    public DateTime? StudentDecidedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
