using System;

namespace CodeNexus.Domain.Entities
{
    public class LearningPathGoal
    {
        public Guid PathId { get; set; }
        public virtual LearningPath LearningPath { get; set; } = null!;

        public Guid GoalId { get; set; }
        public virtual Goals Goal { get; set; } = null!;

        // Weight normalized to 0-1 range (e.g., 0.7 = 70%)
        public decimal Weight { get; set; }
    }
}
