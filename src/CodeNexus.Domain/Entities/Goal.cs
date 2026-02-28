
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
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        
        // Navigation properties
        public virtual ICollection<LearningPath> LearningPaths { get; set; } = new List<LearningPath>();
    }
}
