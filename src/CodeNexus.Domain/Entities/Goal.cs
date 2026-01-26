

namespace CodeNexus.Domain.Entities
{
    public class Goals
    {
        public Guid GoalId { get; set; }
        public Guid UserId { get; set; }
        public virtual User User { get; set; } = null!;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime? TargetDate { get; set; }
        public bool IsCompleted { get; set; } = false;
        public DateTime? CompletedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public virtual ICollection<LearningPath> LearningPaths { get; set; } = new List<LearningPath>();
    }
}
