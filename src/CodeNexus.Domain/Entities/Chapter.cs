using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Domain.Entities
{
    public class Chapter
    {
        public Guid ChapterId { get; set; }
        public Guid PathId { get; set; }
        public virtual LearningPath LearningPath { get; set; } = null!;

        public string Title { get; set; } = string.Empty;
        public string? Content { get; set; }
        public int OrderIndex { get; set; }
        public bool IsCompleted { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        public virtual ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();
        public virtual ICollection<Tasks> Tasks { get; set; } = new List<Tasks>();
    }
}
