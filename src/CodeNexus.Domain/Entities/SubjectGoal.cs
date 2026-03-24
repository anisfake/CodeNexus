using System;

namespace CodeNexus.Domain.Entities
{
    public class SubjectGoal
    {
        public Guid SubjectId { get; set; }
        public virtual Subject Subject { get; set; } = null!;

        public Guid GoalId { get; set; }
        public virtual Goals Goal { get; set; } = null!;
    }
}
