namespace CodeNexus.Domain.Entities
{
    public class DirectMessageReceipt
    {
        public Guid ReceiptId { get; set; }
        public Guid MessageId { get; set; }
        public Guid UserId { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public DateTime? SeenAt { get; set; }

        public virtual DirectMessage Message { get; set; } = null!;
        public virtual User User { get; set; } = null!;
    }
}
