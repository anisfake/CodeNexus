using CodeNexus.Domain.Enums;

namespace CodeNexus.Domain.Entities;

public class UserGoalProgress
{
    public Guid UserGoalProgressId { get; set; }

    public Guid UserId { get; set; }
    public virtual User User { get; set; } = null!;

    public Guid GoalId { get; set; }
    public virtual Goals Goal { get; set; } = null!;

    public Guid LearningPathId { get; set; }
    public virtual LearningPath LearningPath { get; set; } = null!;

    public GoalProgressStatus Status { get; set; } = GoalProgressStatus.NotStarted;
    // Contribution toward this goal from this learning path, in 0-100 scale.
    public decimal ProgressPercent { get; set; } = 0m;

    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
}
