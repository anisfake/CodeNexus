namespace CodeNexus.Infrastructure.Services.AIProviders;

public class AIProviderRuntimeConfig
{
    public string Model { get; set; } = "meta-llama/llama-4-scout-17b-16e-instruct";
    public int MaxTokens { get; set; } = 8192;
    public float Temperature { get; set; } = 0.4f;
    public int RequestTimeoutSeconds { get; set; } = 120;
    public decimal InputCostPer1M { get; set; } = 0m;
    public decimal OutputCostPer1M { get; set; } = 0m;
    public string? BaseUrl { get; set; }
}
