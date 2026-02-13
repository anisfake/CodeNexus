using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Infrastructure.Settings
{
    public class GroqSettings
    {
        public const string SectionName = "GroqSettings";
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int MaxTokens { get; set; } = 8192;
        public float Temperature { get; set; } = 0.3f;
        public int MaxRetries { get; set; } = 3;
        public int RetryDelayMilliseconds { get; set; } = 1000;
        public int RequestTimeoutSeconds { get; set; } = 60;
    }
}
