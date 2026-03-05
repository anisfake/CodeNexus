namespace CodeNexus.Domain.Entities
{
    public class Conversation
    {
        public Guid ConversationId { get; set; }
        public Guid UserId { get; set; }
        public Guid ConfigId { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int MessageCount { get; set; } = 0;
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        public virtual User User { get; set; } = null!;
        public virtual AIProviderConfig Provider { get; set; } = null!;
        public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
    }
}
