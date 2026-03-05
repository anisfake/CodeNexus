using CodeNexus.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Domain.Entities
{
    public class LearningPath
    {
        public Guid PathId { get; set; }
        
        // Student who created this learning path
        public Guid UserId { get; set; }
        public virtual User User { get; set; } = null!;
        
        public Guid SubjectId { get; set; }
        public virtual Subject Subject { get; set; } = null!;

        public Guid? GoalId { get; set; }
        public virtual Goals? Goal { get; set; }

        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Status { get; set; } = "Active";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool CreatedByType { get; set; } // 0: User, 1: AI
        public LanguageSelection Language { get; set; } = LanguageSelection.VietNamese; // Default Vietnamese

        public virtual ICollection<Chapter> Chapters { get; set; } = new List<Chapter>();
        public virtual ICollection<Tasks> Tasks { get; set; } = new List<Tasks>();
    }
}
