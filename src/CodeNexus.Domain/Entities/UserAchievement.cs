namespace CodeNexus.Domain.Entities
{
    public class UserAchievement
    {
        public Guid UserAchievementId { get; set; }
        public Guid UserId { get; set; }
        public Guid AchievementId { get; set; }
        public bool IsUnlocked { get; set; } = false; // True when achievement is completed
        public DateTime? UnlockedAt { get; set; } // When achievement was unlocked
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // When user was initialized

        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual Achievement Achievement { get; set; } = null!;
    }
}