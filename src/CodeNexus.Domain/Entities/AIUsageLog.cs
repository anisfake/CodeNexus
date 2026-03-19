using CodeNexus.Domain.Enums;

namespace CodeNexus.Domain.Entities
{
    public class AIUsageLog
    {
        public Guid UsageLogId { get; set; }
        public AIUsageType UsageType { get; set; }
        public string ProviderName { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public int TotalTokens { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
