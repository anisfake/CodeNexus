using CodeNexus.Domain.Enums;

namespace CodeNexus.Domain.Entities
{
    public class DirectConversation
    {
        public Guid ConversationId { get; set; }
        public Guid? MentorId { get; set; }
        public Guid? StudentId { get; set; }
        public SubjectCategory? Category { get; set; }
        public ChatConversationType ConversationType { get; set; } = ChatConversationType.Direct;
        public string? LastMessagePreview { get; set; }
        public DateTime? LastMessageAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual User? Mentor { get; set; }
        public virtual User? Student { get; set; }
        public virtual ICollection<DirectMessage> Messages { get; set; } = new List<DirectMessage>();
    }
}
