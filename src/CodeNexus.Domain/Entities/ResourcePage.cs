using System;

namespace CodeNexus.Domain.Entities
{
    public class ResourcePage
    {
        public Guid ResourcePageId { get; set; }
        public Guid ResourceId { get; set; }
        public virtual Resource Resource { get; set; } = null!;
        public int PageNumber { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string? ExtractedText { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
