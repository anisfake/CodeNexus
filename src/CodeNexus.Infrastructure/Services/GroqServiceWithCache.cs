using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace CodeNexus.Infrastructure.Services;

public class GroqServiceWithCache : IAIGeneratorService
{
    private readonly HttpClient _httpClient;
    private readonly IApplicationDbContext _context;
    private readonly IAIConfigCacheService _cacheService;
    private readonly IEncryptionService _encryptionService;
    private const string GroqApiUrl = "https://api.groq.com/openai/v1/chat/completions";

    private const string DefaultModel = "meta-llama/llama-4-scout-17b-16e-instruct";
    private const int DefaultMaxTokens = 8192;
    private const float DefaultTemperature = 0.3f;
    private const int DefaultRequestTimeoutSeconds = 60;

    public GroqServiceWithCache(
        HttpClient httpClient,
        IApplicationDbContext context,
        IAIConfigCacheService cacheService,
        IEncryptionService encryptionService)
    {
        _httpClient = httpClient;
        _context = context;
        _cacheService = cacheService;
        _encryptionService = encryptionService;
    }

    public async Task<T> GenerateStructureAsync<T>(string prompt, AIUsageType usageType = AIUsageType.StructureGeneration)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt cannot be empty", nameof(prompt));

        var (apiKey, config) = await GetConfigAsync(usageType);
        var responseText = await CallGroqApiAsync(prompt, apiKey, config, jsonMode: true);
        var jsonContent = ExtractJsonFromResponse(responseText);

        if (string.IsNullOrWhiteSpace(jsonContent))
            throw new InvalidOperationException($"Could not extract JSON from response");

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<T>(jsonContent, options);

            if (result == null)
                throw new InvalidOperationException($"Failed to deserialize to {typeof(T).Name}");

            return result;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"JSON deserialization failed: {ex.Message}", ex);
        }
    }

    public async Task<string> GenerateContentAsync(string prompt, AIUsageType usageType = AIUsageType.StructureGeneration)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt cannot be empty", nameof(prompt));

        var (apiKey, config) = await GetConfigAsync(usageType);
        return await CallGroqApiAsync(prompt, apiKey, config, jsonMode: false);
    }

    private async Task<(string apiKey, GroqConfig config)> GetConfigAsync(AIUsageType usageType)
    {
        var cachedApiKey = await _cacheService.GetApiKeyAsync(usageType);

        if (!string.IsNullOrEmpty(cachedApiKey))
        {
            return (cachedApiKey, new GroqConfig
            {
                Model = DefaultModel,
                MaxTokens = DefaultMaxTokens,
                Temperature = DefaultTemperature,
                RequestTimeoutSeconds = DefaultRequestTimeoutSeconds
            });
        }

        var dbConfig = await _context.AIProviderConfigs
            .FirstOrDefaultAsync(c => c.UsageType == usageType && c.IsActive);

        if (dbConfig == null || string.IsNullOrEmpty(dbConfig.EncryptedApiKey))
        {
            throw new InvalidOperationException($"AI configuration for {usageType} not found in database. Please configure it via AIConfig API.");
        }

        var decryptedApiKey = _encryptionService.Decrypt(dbConfig.EncryptedApiKey);

        var config = ParseConfigJson(dbConfig.ConfigJson);

        await _cacheService.SetApiKeyAsync(usageType, decryptedApiKey, TimeSpan.FromHours(1));

        return (decryptedApiKey, config);
    }

    private GroqConfig ParseConfigJson(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
        {
            return new GroqConfig
            {
                Model = DefaultModel,
                MaxTokens = DefaultMaxTokens,
                Temperature = DefaultTemperature,
                RequestTimeoutSeconds = DefaultRequestTimeoutSeconds
            };
        }

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var config = JsonSerializer.Deserialize<GroqConfig>(configJson, options);

            return config ?? new GroqConfig
            {
                Model = DefaultModel,
                MaxTokens = DefaultMaxTokens,
                Temperature = DefaultTemperature,
                RequestTimeoutSeconds = DefaultRequestTimeoutSeconds
            };
        }
        catch
        {
            return new GroqConfig
            {
                Model = DefaultModel,
                MaxTokens = DefaultMaxTokens,
                Temperature = DefaultTemperature,
                RequestTimeoutSeconds = DefaultRequestTimeoutSeconds
            };
        }
    }

    private async Task<string> CallGroqApiAsync(string prompt, string apiKey, GroqConfig config, bool jsonMode = false)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(config.RequestTimeoutSeconds));

        var messages = new List<object>();

        if (jsonMode)
        {
            messages.Add(new { role = "system", content = "You are a helpful assistant. You must respond with valid JSON only. No markdown, no extra text." });
        }

        messages.Add(new { role = "user", content = prompt });

        var requestBody = new Dictionary<string, object>
        {
            ["model"] = config.Model,
            ["messages"] = messages,
            ["max_tokens"] = config.MaxTokens,
            ["temperature"] = config.Temperature
        };

        if (jsonMode)
        {
            requestBody["response_format"] = new { type = "json_object" };
        }

        var jsonContent = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json"
        );

        using var request = new HttpRequestMessage(HttpMethod.Post, GroqApiUrl)
        {
            Content = jsonContent
        };
        request.Headers.Add("Authorization", $"Bearer {apiKey}");

        var response = await _httpClient.SendAsync(request, cts.Token);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cts.Token);
            throw new InvalidOperationException($"Groq API error ({response.StatusCode}): {errorContent}");
        }

        var responseContent = await response.Content.ReadAsStringAsync(cts.Token);

        try
        {
            var responseJson = JsonDocument.Parse(responseContent);
            var text = responseJson.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrEmpty(text))
                throw new InvalidOperationException("Groq API returned empty response");

            return text;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse Groq response: {ex.Message}", ex);
        }
    }

    private static string ExtractJsonFromResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return null;

        response = response.Trim();

        if (response.Contains("```json"))
        {
            var start = response.IndexOf("```json") + 7;
            var end = response.IndexOf("```", start);
            if (end > start)
                return response.Substring(start, end - start).Trim();
        }

        if (response.Contains("```"))
        {
            var start = response.IndexOf("```") + 3;
            var end = response.IndexOf("```", start);
            if (end > start)
                return response.Substring(start, end - start).Trim();
        }

        var jsonStart = response.IndexOf('{');
        if (jsonStart >= 0)
        {
            var jsonEnd = FindMatchingCloseBrace(response, jsonStart);
            if (jsonEnd > jsonStart)
                return response.Substring(jsonStart, jsonEnd - jsonStart + 1).Trim();
        }

        var arrayStart = response.IndexOf('[');
        if (arrayStart >= 0)
        {
            var arrayEnd = FindMatchingCloseBracket(response, arrayStart);
            if (arrayEnd > arrayStart)
                return response.Substring(arrayStart, arrayEnd - arrayStart + 1).Trim();
        }

        return null;
    }

    private static int FindMatchingCloseBrace(string text, int openBraceIndex)
    {
        int depth = 0;
        bool inString = false;
        bool escapeNext = false;

        for (int i = openBraceIndex; i < text.Length; i++)
        {
            char c = text[i];

            if (escapeNext)
            {
                escapeNext = false;
                continue;
            }

            if (c == '\\')
            {
                escapeNext = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
                continue;

            if (c == '{')
                depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                    return i;
            }
        }

        return -1;
    }

    private static int FindMatchingCloseBracket(string text, int openBracketIndex)
    {
        int depth = 0;
        bool inString = false;
        bool escapeNext = false;

        for (int i = openBracketIndex; i < text.Length; i++)
        {
            char c = text[i];

            if (escapeNext)
            {
                escapeNext = false;
                continue;
            }

            if (c == '\\')
            {
                escapeNext = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
                continue;

            if (c == '[')
                depth++;
            else if (c == ']')
            {
                depth--;
                if (depth == 0)
                    return i;
            }
        }

        return -1;
    }

    private class GroqConfig
    {
        public string Model { get; set; } = DefaultModel;
        public int MaxTokens { get; set; } = DefaultMaxTokens;
        public float Temperature { get; set; } = DefaultTemperature;
        public int RequestTimeoutSeconds { get; set; } = DefaultRequestTimeoutSeconds;
    }
}
