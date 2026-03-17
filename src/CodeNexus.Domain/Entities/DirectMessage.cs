using CodeNexus.Domain.Enums;

namespace CodeNexus.Domain.Entities
{
    public class DirectMessage
    {
        public Guid MessageId { get; set; }
        public Guid ConversationId { get; set; }
        public Guid SenderId { get; set; }
        public string Content { get; set; } = string.Empty;
        public DirectMessageType MessageType { get; set; } = DirectMessageType.Text;
        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        public virtual DirectConversation Conversation { get; set; } = null!;
        public virtual User Sender { get; set; } = null!;
        public virtual ICollection<DirectMessageReceipt> Receipts { get; set; } = new List<DirectMessageReceipt>();
    }
}
