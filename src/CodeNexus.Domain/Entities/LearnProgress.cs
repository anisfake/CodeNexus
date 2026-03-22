namespace CodeNexus.Domain.Entities
{
    public class LearnProgress
    {
        public Guid ProgressId { get; set; }
        public Guid LessonId { get; set; }
        public virtual Lesson Lesson { get; set; } = null!;
        public Guid UserId { get; set; }
        public virtual User User { get; set; } = null!;
        public bool IsLessonContentRead { get; set; } = true;
        public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
