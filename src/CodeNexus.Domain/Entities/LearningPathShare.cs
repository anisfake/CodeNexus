using CodeNexus.Domain.Enums;

namespace CodeNexus.Domain.Entities
{
    public class LearningPathShare
    {
        public Guid ShareId { get; set; }
        public Guid PathId { get; set; }
        public Guid MentorId { get; set; }
        public Guid StudentId { get; set; }
        public LearningPathShareStatus Status { get; set; } = LearningPathShareStatus.Pending;
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
        public DateTime? RespondedAt { get; set; }

        public virtual LearningPath LearningPath { get; set; } = null!;
        public virtual User Mentor { get; set; } = null!;
        public virtual User Student { get; set; } = null!;
    }
}
