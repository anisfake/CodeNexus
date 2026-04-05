namespace CodeNexus.Domain.Entities
{
    public class Conversation
    {
        public Guid ConversationId { get; set; }
        public Guid UserId { get; set; }
        public Guid ConfigId { get; set; }
        public Guid? LearningPathId { get; set; }
        public Guid? ChapterId { get; set; }
        public Guid? LessonId { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int MessageCount { get; set; } = 0;
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        public virtual User User { get; set; } = null!;
        public virtual AIProviderConfig Provider { get; set; } = null!;
        public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
        public virtual ICollection<ConversationSummary> Summaries { get; set; } = new List<ConversationSummary>();
    }
}
