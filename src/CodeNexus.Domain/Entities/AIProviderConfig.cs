namespace CodeNexus.Domain.Entities
{
    public class AIProviderConfig
    {
        public string ProviderName { get; set; } = string.Empty;
        public string EncryptedApiKey { get; set; } = string.Empty;
        public string ConfigJson { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        public virtual ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
    }
}
