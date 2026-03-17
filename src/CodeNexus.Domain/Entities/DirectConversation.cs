namespace CodeNexus.Domain.Entities
{
    public class DirectConversation
    {
        public Guid ConversationId { get; set; }
        public Guid MentorId { get; set; }
        public Guid StudentId { get; set; }
        public string? LastMessagePreview { get; set; }
        public DateTime? LastMessageAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual User Mentor { get; set; } = null!;
        public virtual User Student { get; set; } = null!;
        public virtual ICollection<DirectMessage> Messages { get; set; } = new List<DirectMessage>();
    }
}
