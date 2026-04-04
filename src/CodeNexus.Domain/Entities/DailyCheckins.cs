using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Domain.Entities
{
    public class DailyCheckins
    {
        public Guid CheckinId { get; set; }
        public Guid UserId { get; set; }
        public virtual User User { get; set; } = null!;

        public DateTime CheckinDate { get; set; }
        public string? Mood { get; set; }
        public int? Productivity { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
