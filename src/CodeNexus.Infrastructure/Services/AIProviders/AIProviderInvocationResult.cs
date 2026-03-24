namespace CodeNexus.Infrastructure.Services.AIProviders;

public class AIProviderInvocationResult
{
    public string Content { get; set; } = string.Empty;
    public string? FinishReason { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int TotalTokens { get; set; }
}
