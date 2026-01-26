using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Domain.Entities
{
    public class AuditLog
    {
        public Guid LogId { get; set; }
        public Guid? UserId { get; set; }
        public virtual User? User { get; set; }

        public string Action { get; set; } = string.Empty;
        public string? TableName { get; set; }
        public Guid? RecordId { get; set; }
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string? IPAddress { get; set; }
    }
}
