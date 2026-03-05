using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Domain.Entities
{
    public class AISummary
    {
        public Guid SummaryId { get; set; }
        public string? ModelName { get; set; }
        public string? Version { get; set; }
        public string? Configuration { get; set; }

        public Guid ResourceId { get; set; }
        public virtual Resource Resource { get; set; } = null!;

        public string Title { get; set; } = string.Empty;
        public string? Summary { get; set; }
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<Quiz> Quizzes { get; set; } = new List<Quiz>();
    }
}
