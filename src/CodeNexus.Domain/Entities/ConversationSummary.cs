namespace CodeNexus.Domain.Entities
{
    public class ConversationSummary
    {
        public Guid SummaryId { get; set; }
        public Guid ConversationId { get; set; }
        public string SummaryContent { get; set; } = string.Empty;
        public int MessageCount { get; set; }
        public DateTime? StartMessageCreatedAt { get; set; }
        public DateTime? EndMessageCreatedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual Conversation Conversation { get; set; } = null!;
    }
}
