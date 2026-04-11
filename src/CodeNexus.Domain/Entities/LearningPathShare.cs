using CodeNexus.Domain.Enums;

namespace CodeNexus.Domain.Entities
{
    public class LearningPathShare
    {
        public Guid ShareId { get; set; }
        public Guid PathId { get; set; }
        public Guid MentorId { get; set; }
        public Guid StudentId { get; set; }
        public Guid? AcceptedPathId { get; set; }
        public decimal? SourceVersionAtAccept { get; set; }
        public decimal? IgnoredSourceVersion { get; set; }
        public decimal? LastNotifiedSourceVersion { get; set; }
        public bool IsTrackingEnabled { get; set; } = true;
        public string? InvalidatedReason { get; set; }
        public LearningPathShareStatus Status { get; set; } = LearningPathShareStatus.Pending;
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
        public DateTime? RespondedAt { get; set; }

        public virtual LearningPath LearningPath { get; set; } = null!;
        public virtual LearningPath? AcceptedPath { get; set; }
        public virtual User Mentor { get; set; } = null!;
        public virtual User Student { get; set; } = null!;
    }
}
