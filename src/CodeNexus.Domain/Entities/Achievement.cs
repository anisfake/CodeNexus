using CodeNexus.Domain.Enums;

namespace CodeNexus.Domain.Entities
{
    public class Achievement
    {
        public Guid AchievementId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty; 
        public AchievementCategory Category { get; set; }
        public int Points { get; set; } = 0;
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<UserAchievement> UserAchievements { get; set; } = new List<UserAchievement>();
    }
}