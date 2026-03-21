using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeNexus.Infrastructure.Services;

public class GroqServiceWithCache : IAIGeneratorService
{
    private readonly HttpClient _httpClient;
    private readonly IApplicationDbContext _context;
    private readonly IAIConfigCacheService _cacheService;
    private readonly IEncryptionService _encryptionService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISubscriptionAccessService _subscriptionAccessService;
    private const string GroqApiUrl = "https://api.groq.com/openai/v1/chat/completions";

    private const string DefaultModel = "meta-llama/llama-4-scout-17b-16e-instruct";
    private const int DefaultMaxTokens = 8192;
    private const float DefaultTemperature = 0.4f;
    private const int DefaultRequestTimeoutSeconds = 120;

    public GroqServiceWithCache(
        HttpClient httpClient,
        IApplicationDbContext context,
        IAIConfigCacheService cacheService,
        IEncryptionService encryptionService,
        ICurrentUserService currentUserService,
        ISubscriptionAccessService subscriptionAccessService)
    {
        _httpClient = httpClient;
        _context = context;
        _cacheService = cacheService;
        _encryptionService = encryptionService;
        _currentUserService = currentUserService;
        _subscriptionAccessService = subscriptionAccessService;
    }

    public async Task<T> GenerateStructureAsync<T>(string prompt, AIUsageType usageType = AIUsageType.StructureGeneration)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt cannot be empty", nameof(prompt));

        const int maxRetries = 3;
        Exception lastException = null;
        var allAttempts = new List<string>();

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var (apiKey, config, providerName) = await GetConfigAsync(usageType);

                var adjustedConfig = attempt == 1 ? config : AdjustConfigForAttempt(config, attempt);

                var responseText = await CallGroqApiAsync(prompt, apiKey, adjustedConfig, usageType, providerName, jsonMode: true);
                allAttempts.Add($"Attempt {attempt}: {responseText?.Substring(0, Math.Min(200, responseText?.Length ?? 0))}...");

                var jsonContent = ExtractJsonFromResponse(responseText);

                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    throw new InvalidOperationException($"Could not extract JSON from response. Raw response: {responseText?.Substring(0, Math.Min(500, responseText?.Length ?? 0))}...");
                }

                var result = DeserializeWithFallback<T>(jsonContent);

                if (result == null)
                    throw new InvalidOperationException($"Failed to deserialize to {typeof(T).Name}");

                return result;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                lastException = ex;
                var delay = Math.Min(500 * attempt, 2000);
                await Task.Delay(delay);
                continue;
            }
            catch (Exception ex)
            {
                lastException = ex;
                break;
            }
        }

        try
        {
            var fallbackResult = await GenerateFallbackStructure<T>(prompt, usageType);
            if (fallbackResult != null)
            {
                Console.WriteLine($"WARNING: Used fallback structure after {maxRetries} failed attempts. All attempts: {string.Join("; ", allAttempts)}");
                return fallbackResult;
            }
        }
        catch (Exception fallbackEx)
        {
            Console.WriteLine($"Fallback also failed: {fallbackEx.Message}");
        }

        throw new InvalidOperationException($"CRITICAL: Failed to generate structure after {maxRetries} attempts and fallback failed. Last error: {lastException?.Message}. All attempts: {string.Join("; ", allAttempts)}", lastException);
    }

    public async Task<string> GenerateContentAsync(string prompt, AIUsageType usageType = AIUsageType.StructureGeneration)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt cannot be empty", nameof(prompt));

        var (apiKey, config, providerName) = await GetConfigAsync(usageType);
        return await CallGroqApiAsync(prompt, apiKey, config, usageType, providerName, jsonMode: false);
    }

    private async Task<(string apiKey, GroqConfig config, string providerName)> GetConfigAsync(AIUsageType usageType)
    {
        var preferredTier = await ResolvePreferredTierAsync();
        var selectedConfig = await ResolveConfigByTierAsync(usageType, preferredTier)
            ?? await ResolveConfigByTierAsync(usageType, preferredTier == AIAccessTier.Paid ? AIAccessTier.Free : AIAccessTier.Paid);

        if (selectedConfig == null)
        {
            throw new InvalidOperationException($"AI configuration for {usageType} not found in database. Please configure it via AIConfig API.");
        }

        var cachedApiKey = await _cacheService.GetApiKeyAsync(usageType, selectedConfig.AccessTier, CancellationToken.None);
        var apiKey = string.IsNullOrWhiteSpace(cachedApiKey)
            ? _encryptionService.Decrypt(selectedConfig.EncryptedApiKey)
            : cachedApiKey;

        if (string.IsNullOrWhiteSpace(cachedApiKey))
        {
            await _cacheService.SetApiKeyAsync(usageType, selectedConfig.AccessTier, apiKey, TimeSpan.FromHours(1));
        }

        var config = ParseConfigJson(selectedConfig.ConfigJson);
        return (apiKey, config, string.IsNullOrWhiteSpace(selectedConfig.ProviderName) ? "Groq" : selectedConfig.ProviderName);
    }

    private async Task<AIAccessTier> ResolvePreferredTierAsync()
    {
        try
        {
            var userId = _currentUserService.GetUserId();
            var canUsePaid = await _subscriptionAccessService.CanUsePaidModelsAsync(userId);
            return canUsePaid ? AIAccessTier.Paid : AIAccessTier.Free;
        }
        catch
        {
            return AIAccessTier.Free;
        }
    }

    private async Task<CodeNexus.Domain.Entities.AIProviderConfig?> ResolveConfigByTierAsync(AIUsageType usageType, AIAccessTier tier)
    {
        return await _context.AIProviderConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UsageType == usageType && c.AccessTier == tier && c.IsActive, CancellationToken.None);
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

    private async Task<string> CallGroqApiAsync(
        string prompt,
        string apiKey,
        GroqConfig config,
        AIUsageType usageType,
        string providerName,
        bool jsonMode = false)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(config.RequestTimeoutSeconds));

        var messages = new List<object>();

        if (jsonMode)
        {
            messages.Add(new { role = "system", content = "You are a helpful assistant. You must respond with valid, complete JSON only. No markdown, no extra text, no truncated responses. Ensure the JSON is properly closed with all brackets and braces." });
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
            await TryLogUsageAsync(responseJson, usageType, providerName, config);
            var text = responseJson.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrEmpty(text))
                throw new InvalidOperationException("Groq API returned empty response");

            var finishReason = responseJson.RootElement
                .GetProperty("choices")[0]
                .GetProperty("finish_reason")
                .GetString();

            if (finishReason == "length")
            {
                throw new InvalidOperationException("Response was truncated due to max_tokens limit. Consider increasing max_tokens or simplifying the prompt.");
            }

            return text;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse Groq response: {ex.Message}. Response: {responseContent.Substring(0, Math.Min(500, responseContent.Length))}...", ex);
        }
    }

    private async Task TryLogUsageAsync(JsonDocument responseJson, AIUsageType usageType, string providerName, GroqConfig config)
    {
        try
        {
            if (!responseJson.RootElement.TryGetProperty("usage", out var usage))
            {
                return;
            }

            var inputTokens = usage.TryGetProperty("prompt_tokens", out var promptTokens)
                ? promptTokens.GetInt32()
                : 0;
            var outputTokens = usage.TryGetProperty("completion_tokens", out var completionTokens)
                ? completionTokens.GetInt32()
                : 0;
            var totalTokens = usage.TryGetProperty("total_tokens", out var total)
                ? total.GetInt32()
                : inputTokens + outputTokens;

            var costUsd = CalculateCostUsd(config, inputTokens, outputTokens);

            _context.AIUsageLogs.Add(new CodeNexus.Domain.Entities.AIUsageLog
            {
                UsageType = usageType,
                ProviderName = providerName,
                Model = config.Model,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                TotalTokens = totalTokens,
                CostUsd = costUsd,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
        }
        catch
        {
            // Avoid blocking AI response if logging fails.
        }
    }

    private static decimal CalculateCostUsd(GroqConfig config, int inputTokens, int outputTokens)
    {
        if (config.InputCostPer1M <= 0 && config.OutputCostPer1M <= 0)
        {
            return 0m;
        }

        const decimal OneMillion = 1_000_000m;
        var inputCost = (inputTokens / OneMillion) * config.InputCostPer1M;
        var outputCost = (outputTokens / OneMillion) * config.OutputCostPer1M;
        return Math.Round(inputCost + outputCost, 6);
    }

    private static GroqConfig AdjustConfigForAttempt(GroqConfig baseConfig, int attempt)
    {
        return new GroqConfig
        {
            Model = baseConfig.Model,
            MaxTokens = Math.Min(baseConfig.MaxTokens + (attempt * 1024), 24576),
            Temperature = Math.Min(baseConfig.Temperature + (attempt * 0.05f), 0.6f),
            RequestTimeoutSeconds = baseConfig.RequestTimeoutSeconds + (attempt * 15)
        };
    }

    private T DeserializeWithFallback<T>(string jsonContent)
    {
        var strategies = new List<JsonSerializerOptions>
        {
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            },
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            },
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                PropertyNamingPolicy = null,
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            }
        };

        foreach (var options in strategies)
        {
            try
            {
                var result = JsonSerializer.Deserialize<T>(jsonContent, options);
                if (result != null)
                    return result;
            }
            catch (JsonException)
            {
                continue;
            }
        }

        throw new InvalidOperationException($"All deserialization strategies failed for JSON: {jsonContent.Substring(0, Math.Min(300, jsonContent.Length))}...");
    }

    private async Task<T?> GenerateFallbackStructure<T>(string prompt, AIUsageType usageType)
    {
        if (typeof(T).Name.Contains("LearningPathSkeleton"))
        {
            return await GenerateFallbackLearningPath<T>(prompt);
        }

        return default(T);
    }

    private async Task<T?> GenerateFallbackLearningPath<T>(string prompt)
    {
        var subjectMatch = System.Text.RegularExpressions.Regex.Match(prompt, @"Subject:\s*([^\n\r]+)");
        var goalMatch = System.Text.RegularExpressions.Regex.Match(prompt, @"Goal:\s*([^\n\r]+)");

        var subject = subjectMatch.Success ? subjectMatch.Groups[1].Value.Trim() : "Programming";
        var goal = goalMatch.Success ? goalMatch.Groups[1].Value.Trim() : "Learn Programming";

        var fallbackJson = $@"{{
  ""title"": ""Lộ trình học {subject}"",
  ""description"": ""Lộ trình học cơ bản về {subject} - được tạo tự động"",
  ""chapters"": [
    {{
      ""chapterId"": ""{Guid.NewGuid()}"",
      ""title"": ""Giới thiệu cơ bản"",
      ""content"": ""Tìm hiểu các khái niệm cơ bản"",
      ""orderIndex"": 0,
      ""lessons"": [
        {{
          ""lessonId"": ""{Guid.NewGuid()}"",
          ""title"": ""Bài học đầu tiên"",
          ""content"": ""Nội dung bài học cơ bản"",
          ""quizzes"": []
        }}
      ],
      ""tasks"": []
    }},
    {{
      ""chapterId"": ""{Guid.NewGuid()}"",
      ""title"": ""Thực hành cơ bản"",
      ""content"": ""Áp dụng kiến thức vào thực tế"",
      ""orderIndex"": 1,
      ""lessons"": [
        {{
          ""lessonId"": ""{Guid.NewGuid()}"",
          ""title"": ""Bài thực hành"",
          ""content"": ""Thực hành với các ví dụ đơn giản"",
          ""quizzes"": []
        }}
      ],
      ""tasks"": []
    }}
  ]
}}";

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<T>(fallbackJson, options);
            return result;
        }
        catch
        {
            return default(T);
        }
    }

    private static bool IsValidJson(string jsonString)
    {
        if (string.IsNullOrWhiteSpace(jsonString))
            return false;

        try
        {
            using var document = JsonDocument.Parse(jsonString);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string ExtractJsonFromResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return null;

        response = response.Trim();

        var prefixesToRemove = new[] { "Here's the JSON:", "JSON:", "Response:", "```json", "```" };
        foreach (var prefix in prefixesToRemove)
        {
            if (response.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                response = response.Substring(prefix.Length).Trim();
            }
        }

        if (response.Contains("```json"))
        {
            var start = response.IndexOf("```json") + 7;
            var end = response.IndexOf("```", start);
            if (end > start)
            {
                var extracted = response.Substring(start, end - start).Trim();
                if (IsValidJson(extracted))
                    return extracted;
            }
        }

        if (response.Contains("```"))
        {
            var start = response.IndexOf("```") + 3;
            var end = response.IndexOf("```", start);
            if (end > start)
            {
                var extracted = response.Substring(start, end - start).Trim();
                if (IsValidJson(extracted))
                    return extracted;
            }
        }

        var jsonStart = response.IndexOf('{');
        if (jsonStart >= 0)
        {
            var jsonEnd = FindMatchingCloseBrace(response, jsonStart);
            if (jsonEnd > jsonStart)
            {
                var extracted = response.Substring(jsonStart, jsonEnd - jsonStart + 1).Trim();
                if (IsValidJson(extracted))
                    return extracted;
            }
        }

        var arrayStart = response.IndexOf('[');
        if (arrayStart >= 0)
        {
            var arrayEnd = FindMatchingCloseBracket(response, arrayStart);
            if (arrayEnd > arrayStart)
            {
                var extracted = response.Substring(arrayStart, arrayEnd - arrayStart + 1).Trim();
                if (IsValidJson(extracted))
                    return extracted;
            }
        }

        if ((response.StartsWith("{") && response.EndsWith("}")) ||
            (response.StartsWith("[") && response.EndsWith("]")))
        {
            if (IsValidJson(response))
                return response;
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
        public decimal InputCostPer1M { get; set; } = 0m;
        public decimal OutputCostPer1M { get; set; } = 0m;
    }
}
