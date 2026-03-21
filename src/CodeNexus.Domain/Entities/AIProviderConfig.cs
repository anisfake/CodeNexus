using CodeNexus.Domain.Enums;

namespace CodeNexus.Domain.Entities
{
    public class AIProviderConfig
    {
        public Guid ConfigId { get; set; }
        public string ProviderName { get; set; } = string.Empty;
        public AIUsageType UsageType { get; set; } = AIUsageType.StructureGeneration;
        public AIAccessTier AccessTier { get; set; } = AIAccessTier.Free;
        public string EncryptedApiKey { get; set; } = string.Empty;
        public string ConfigJson { get; set; } = string.Empty;
        public bool IsActive { get; set; } = false;
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        public virtual ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
    }
}
