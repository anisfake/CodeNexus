using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Domain.Entities
{
    public class Tasks
    {
        public Guid TaskId { get; set; }
        public Guid ChapterId { get; set; }
        public virtual Chapter Chapter { get; set; } = null!;
        public Guid PathId { get; set; }
        public virtual LearningPath LearningPath { get; set; } = null!;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime? DueDate { get; set; }
        public string? Priority { get; set; }
        public string Status { get; set; } = "Pending";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? CompletedAt { get; set; }
        public DateTime? GoalDeadline { get; set; }
        public string? GoalContent { get; set; }
        public virtual ICollection<FocusSession> FocusSessions { get; set; } = new List<FocusSession>();
    }
}
