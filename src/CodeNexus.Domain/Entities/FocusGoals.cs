using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Domain.Entities
{
    public class FocusGoals
    {
        public Guid FocusGoalId { get; set; }
        public Guid SessionId { get; set; }
        public virtual FocusSession FocusSession { get; set; } = null!;

        public DateTime Deadline { get; set; }
        public string Content { get; set; } = string.Empty;
    }
}
