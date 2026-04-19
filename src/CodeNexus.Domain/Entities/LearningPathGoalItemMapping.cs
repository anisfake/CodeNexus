using CodeNexus.Domain.Enums;

namespace CodeNexus.Domain.Entities;

public class LearningPathGoalItemMapping
{
    public Guid MappingId { get; set; }

    public Guid PathId { get; set; }
    public virtual LearningPath LearningPath { get; set; } = null!;

    public Guid GoalId { get; set; }
    public virtual Goals Goal { get; set; } = null!;

    public Guid ItemId { get; set; }
    public LearningPathGoalItemType ItemType { get; set; }

    // 0..1 score produced by semantic matching.
    public decimal RelevanceScore { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
