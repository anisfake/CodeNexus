using CodeNexus.Domain.Enums;
using System.Text.Json.Serialization;

namespace CodeNexus.API.Models.Requests
{
    public class UploadResourceRequest
    {
        public IFormFile? File { get; set; }
        public string Title { get; set; } = string.Empty;
        public ResourceType Type { get; set; }
        public string? Url { get; set; }
        public string? Description { get; set; }
        public Guid SubjectId { get; set; }
    }
}
