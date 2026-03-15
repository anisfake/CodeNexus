using System;

namespace CodeNexus.Domain.Entities
{
    public class GoalMapping
    {
        public Guid MappingId { get; set; }

        public Guid UserGoalId { get; set; }
        public virtual Goals UserGoal { get; set; } = null!;

        public Guid SystemGoalId { get; set; }
        public virtual Goals SystemGoal { get; set; } = null!;

        public decimal Confidence { get; set; }
        public bool VerifiedByAI { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
