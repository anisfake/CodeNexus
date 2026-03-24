namespace CodeNexus.Infrastructure.Services.AIProviders;

public interface IAIProviderAdapter
{
    bool CanHandle(string providerName);

    Task<AIProviderInvocationResult> GenerateAsync(
        string prompt,
        string apiKey,
        AIProviderRuntimeConfig config,
        bool jsonMode,
        CancellationToken cancellationToken);
}
