using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Domain.Entities
{
    public class FocusSession
    {
        public Guid SessionId { get; set; }
        public Guid TaskId { get; set; }
        public virtual Tasks Task { get; set; } = null!;
        public string? Title { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int Duration { get; set; }
        public string? SessionType { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? GoalDeadline { get; set; }
        public string? GoalContent { get; set; }
        public virtual ICollection<Note> Notes { get; set; } = new List<Note>();
        public virtual DailyCheckins? DailyCheckin { get; set; }
    }
}
