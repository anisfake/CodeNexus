using CodeNexus.Domain.Enums;

namespace CodeNexus.Domain.Entities
{
    public class DirectMessage
    {
        public Guid MessageId { get; set; }
        public Guid ConversationId { get; set; }
        public Guid SenderId { get; set; }
        public Guid? ReplyToMessageId { get; set; }
        public string Content { get; set; } = string.Empty;
        public DirectMessageType MessageType { get; set; } = DirectMessageType.Text;
        public Guid? LearningPathShareId { get; set; }
        public Guid? TaskReviewId { get; set; }
        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        public virtual DirectConversation Conversation { get; set; } = null!;
        public virtual User Sender { get; set; } = null!;
        public virtual DirectMessage? ReplyToMessage { get; set; }
        public virtual ICollection<DirectMessage> Replies { get; set; } = new List<DirectMessage>();
        public virtual LearningPathShare? LearningPathShare { get; set; }
        public virtual TaskReview? TaskReview { get; set; }
        public virtual ICollection<DirectMessageReceipt> Receipts { get; set; } = new List<DirectMessageReceipt>();
    }
}
