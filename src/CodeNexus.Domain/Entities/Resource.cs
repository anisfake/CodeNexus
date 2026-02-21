using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CodeNexus.Domain.Enums;

namespace CodeNexus.Domain.Entities
{
    public class Resource
    {
        public Guid ResourceId { get; set; }
        public Guid UserId { get; set; }
        public virtual User User { get; set; } = null!;
        public Guid SubjectId { get; set; }
        public virtual Subject Subject { get; set; } = null!;
        public string Title { get; set; } = string.Empty;
        public ResourceType Type { get; set; }
        public string? URL { get; set; }
        public string? FilePath { get; set; }
        public string? OriginalFileName { get; set; }
        public string? Description { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<AISummary> AISummaries { get; set; } = new List<AISummary>();
    }
}
