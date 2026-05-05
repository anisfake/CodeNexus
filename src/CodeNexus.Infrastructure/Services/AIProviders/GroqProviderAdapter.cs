using System.Text;
using System.Text.Json;

namespace CodeNexus.Infrastructure.Services.AIProviders;

public class GroqProviderAdapter : IAIProviderAdapter
{
    private const string DefaultGroqApiUrl = "https://api.groq.com/openai/v1/chat/completions";
    private readonly HttpClient _httpClient;

    public GroqProviderAdapter(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = Timeout.InfiniteTimeSpan;
    }

    public bool CanHandle(string providerName)
    {
        return providerName.Contains("groq", StringComparison.OrdinalIgnoreCase);
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

        var messages = new List<object>();
        if (jsonMode)
        {
            messages.Add(new
            {
                role = "system",
                content = "You are a helpful assistant. You must respond with valid, complete JSON only."
            });
        }

        messages.Add(new { role = "user", content = prompt });

        var requestBody = new Dictionary<string, object>
        {
            ["model"] = config.Model,
            ["messages"] = messages,
            ["max_tokens"] = config.MaxTokens,
            ["temperature"] = config.Temperature,
            ["top_p"] = 0.9,
            ["stream"] = false
        };

        if (jsonMode)
        {
            requestBody["response_format"] = new { type = "json_object" };
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, string.IsNullOrWhiteSpace(config.BaseUrl) ? DefaultGroqApiUrl : config.BaseUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Authorization", $"Bearer {apiKey}");

        var response = await _httpClient.SendAsync(request, cts.Token);
        var responseContent = await response.Content.ReadAsStringAsync(cts.Token);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Groq API error ({response.StatusCode}): {responseContent}");
        }

        using var responseJson = JsonDocument.Parse(responseContent);
        var content = responseJson.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("Groq API returned empty response");
        }

        var finishReason = responseJson.RootElement
            .GetProperty("choices")[0]
            .GetProperty("finish_reason")
            .GetString();

        int inputTokens = 0;
        int outputTokens = 0;
        int totalTokens = 0;
        if (responseJson.RootElement.TryGetProperty("usage", out var usage))
        {
            inputTokens = usage.TryGetProperty("prompt_tokens", out var promptTokens) ? promptTokens.GetInt32() : 0;
            outputTokens = usage.TryGetProperty("completion_tokens", out var completionTokens) ? completionTokens.GetInt32() : 0;
            totalTokens = usage.TryGetProperty("total_tokens", out var total) ? total.GetInt32() : inputTokens + outputTokens;
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
