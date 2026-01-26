using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Domain.Entities
{
    public class TaskGoals
    {
        public Guid TaskGoalId { get; set; }
        public Guid TaskId { get; set; }
        public virtual Tasks Task { get; set; } = null!;
        public DateTime Deadline { get; set; }
        public string Content { get; set; } = string.Empty;
    }
}
