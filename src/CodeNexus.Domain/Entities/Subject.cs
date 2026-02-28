using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Domain.Entities
{
    public class Subject
    {
        public Guid SubjectId { get; set; }
        public Guid CreatedByUserId { get; set; }
        public virtual User CreatedByUser { get; set; } = null!;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Color { get; set; }
        public string? Icon { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        public virtual ICollection<LearningPath> LearningPaths { get; set; } = new List<LearningPath>();
        public virtual ICollection<Resource> Resources { get; set; } = new List<Resource>();
    }
}
