using System.Text;
using System.Text.Json;

namespace CodeNexus.Infrastructure.Services.AIProviders;

public class GeminiProviderAdapter : IAIProviderAdapter
{
    private const string DefaultGeminiBaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";
    private readonly HttpClient _httpClient;

    public GeminiProviderAdapter(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public bool CanHandle(string providerName)
    {
        return providerName.Contains("gemini", StringComparison.OrdinalIgnoreCase)
            || providerName.Contains("google", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<AIProviderInvocationResult> GenerateAsync(
        string prompt,
        string apiKey,
        AIProviderRuntimeConfig config,
        bool jsonMode,
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(config.RequestTimeoutSeconds));

        var baseUrl = string.IsNullOrWhiteSpace(config.BaseUrl) ? DefaultGeminiBaseUrl : config.BaseUrl;
        var endpoint = $"{baseUrl.TrimEnd('/')}/{config.Model}:generateContent?key={Uri.EscapeDataString(apiKey)}";

        var generationConfig = new Dictionary<string, object>
        {
            ["temperature"] = config.Temperature,
            ["maxOutputTokens"] = config.MaxTokens,
            ["topP"] = 0.9
        };

        if (jsonMode)
        {
            generationConfig["responseMimeType"] = "application/json";
        }

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = prompt } }
                }
            },
            generationConfig
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
        };

        var response = await _httpClient.SendAsync(request, cts.Token);
        var responseContent = await response.Content.ReadAsStringAsync(cts.Token);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Gemini API error ({response.StatusCode}): {responseContent}");
        }

        using var responseJson = JsonDocument.Parse(responseContent);

        var content = responseJson.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("Gemini API returned empty response");
        }

        var finishReason = responseJson.RootElement
            .GetProperty("candidates")[0]
            .TryGetProperty("finishReason", out var fr)
            ? fr.GetString()
            : null;

        int inputTokens = 0;
        int outputTokens = 0;
        int totalTokens = 0;
        if (responseJson.RootElement.TryGetProperty("usageMetadata", out var usage))
        {
            inputTokens = usage.TryGetProperty("promptTokenCount", out var promptTokens) ? promptTokens.GetInt32() : 0;
            outputTokens = usage.TryGetProperty("candidatesTokenCount", out var output) ? output.GetInt32() : 0;
            totalTokens = usage.TryGetProperty("totalTokenCount", out var total) ? total.GetInt32() : inputTokens + outputTokens;
        }

        return new AIProviderInvocationResult
        {
            Content = content,
            FinishReason = finishReason,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            TotalTokens = totalTokens
        };
    }
}
