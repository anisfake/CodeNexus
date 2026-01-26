using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Domain.Entities
{
    public class ChatMessages
    {
        public Guid MessageId { get; set; }

        public Guid? InteractionId { get; set; }
        public virtual AIInteraction? Interaction { get; set; }

        public Guid? UserId { get; set; }
        public virtual User? User { get; set; }

        public string Sender { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
