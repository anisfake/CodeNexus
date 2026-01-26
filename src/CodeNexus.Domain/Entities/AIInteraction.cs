using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Domain.Entities
{
    public class AIInteraction
    {
        public Guid InteractionId { get; set; }
        public string? ModelName { get; set; }
        public string? Version { get; set; }
        public string? Configuration { get; set; }
        public string Query { get; set; } = string.Empty;
        public string? Response { get; set; }
        public string? Context { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public virtual ICollection<ChatMessages> ChatMessages { get; set; } = new List<ChatMessages>();
    }
}
