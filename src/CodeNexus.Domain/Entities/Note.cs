using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Domain.Entities
{
    public class Note
    {
        public Guid NoteId { get; set; }
        public Guid? SessionId { get; set; }
        public virtual FocusSession? FocusSession { get; set; }

        public string? Title { get; set; }
        public string? Content { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        public virtual ICollection<NoteTags> NoteTags { get; set; } = new List<NoteTags>();
    }
}
