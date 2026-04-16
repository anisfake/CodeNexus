
using CodeNexus.Domain.Enums;

namespace CodeNexus.Domain.Entities
{
    public class Goals
    {
        public Guid GoalId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsSystemDefined { get; set; } = false; 
        public Guid? CreatedByUserId { get; set; } 
        public virtual User? CreatedByUser { get; set; }
        public GoalDuration Duration { get; set; } = GoalDuration.OneMonth;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        
        // Computed property
        public int DurationInDays => (int)Duration;
        
        // Navigation properties
        public virtual ICollection<LearningPathGoal> LearningPathGoals { get; set; } = new List<LearningPathGoal>();
        public virtual ICollection<SubjectGoal> SubjectGoals { get; set; } = new List<SubjectGoal>();
        public virtual ICollection<GoalMapping> UserGoalMappings { get; set; } = new List<GoalMapping>();
        public virtual ICollection<GoalMapping> SystemGoalMappings { get; set; } = new List<GoalMapping>();
        public virtual ICollection<UserGoalProgress> UserGoalProgresses { get; set; } = new List<UserGoalProgress>();
    }
}
