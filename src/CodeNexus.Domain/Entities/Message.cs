namespace CodeNexus.Domain.Entities
{
    public class Message
    {
        public Guid MessageId { get; set; }
        public Guid ConversationId { get; set; }
        public string Content { get; set; } = string.Empty;
        public int? InputTokens { get; set; }
        public int? OutputTokens { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual Conversation Conversation { get; set; } = null!;
    }
}
