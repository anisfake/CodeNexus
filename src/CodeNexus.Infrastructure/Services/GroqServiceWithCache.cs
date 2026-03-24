using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Domain.Enums;
using CodeNexus.Infrastructure.Services.AIProviders;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeNexus.Domain.Entities;

namespace CodeNexus.Infrastructure.Services;

public class GroqServiceWithCache : IAIGeneratorService
{
    private readonly HttpClient _httpClient;
    private readonly IApplicationDbContext _context;
    private readonly IAIConfigCacheService _cacheService;
    private readonly IEncryptionService _encryptionService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISubscriptionAccessService _subscriptionAccessService;
    private readonly IReadOnlyCollection<IAIProviderAdapter> _providerAdapters;

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
        ISubscriptionAccessService subscriptionAccessService,
        IEnumerable<IAIProviderAdapter>? providerAdapters = null)
    {
        _httpClient = httpClient;
        _context = context;
        _cacheService = cacheService;
        _encryptionService = encryptionService;
        _currentUserService = currentUserService;
        _subscriptionAccessService = subscriptionAccessService;
        _providerAdapters = providerAdapters?.ToList()
            ?? new List<IAIProviderAdapter>
            {
                new GroqProviderAdapter(_httpClient),
                new GeminiProviderAdapter(_httpClient),
                new MistralProviderAdapter(_httpClient)
            };
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

                var responseText = await CallProviderApiAsync(prompt, apiKey, adjustedConfig, usageType, providerName, jsonMode: true);
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
        return await CallProviderApiAsync(prompt, apiKey, config, usageType, providerName, jsonMode: false);
    }

    private async Task<(string apiKey, AIProviderRuntimeConfig config, string providerName)> GetConfigAsync(AIUsageType usageType)
    {
        var preferredTier = await ResolvePreferredTierAsync();
        var selectedConfig = await ResolveConfigWithTierPreferenceAsync(usageType, preferredTier);

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

    private async Task<AIProviderConfig?> ResolveConfigWithTierPreferenceAsync(
        AIUsageType usageType,
        AIAccessTier preferredTier)
    {
        var config = await ResolveConfigByTierAsync(usageType, preferredTier);
        if (config != null)
            return config;

        config = await ResolveAnyUsageConfigByTierAsync(preferredTier);
        if (config != null)
            return config;

        var secondaryTier = preferredTier == AIAccessTier.Paid ? AIAccessTier.Free : AIAccessTier.Paid;

        config = await ResolveConfigByTierAsync(usageType, secondaryTier);
        if (config != null)
            return config;

        return await ResolveAnyUsageConfigByTierAsync(secondaryTier);
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

    private async Task<AIProviderConfig?> ResolveConfigByTierAsync(AIUsageType usageType, AIAccessTier tier)
    {
        return await _context.AIProviderConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UsageType == usageType && c.AccessTier == tier && c.IsActive, CancellationToken.None);
    }

    private async Task<AIProviderConfig?> ResolveAnyUsageConfigByTierAsync(AIAccessTier tier)
    {
        return await _context.AIProviderConfigs
            .AsNoTracking()
            .Where(c => c.AccessTier == tier && c.IsActive)
            .OrderBy(c => c.UsageType == AIUsageType.StructureGeneration ? 0 : 1)
            .ThenByDescending(c => c.LastUpdated)
            .FirstOrDefaultAsync(CancellationToken.None);
    }

    private AIProviderRuntimeConfig ParseConfigJson(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
        {
            return new AIProviderRuntimeConfig
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
            var config = JsonSerializer.Deserialize<AIProviderRuntimeConfig>(configJson, options);

            return config ?? new AIProviderRuntimeConfig
            {
                Model = DefaultModel,
                MaxTokens = DefaultMaxTokens,
                Temperature = DefaultTemperature,
                RequestTimeoutSeconds = DefaultRequestTimeoutSeconds
            };
        }
        catch
        {
            return new AIProviderRuntimeConfig
            {
                Model = DefaultModel,
                MaxTokens = DefaultMaxTokens,
                Temperature = DefaultTemperature,
                RequestTimeoutSeconds = DefaultRequestTimeoutSeconds
            };
        }
    }

    private async Task<string> CallProviderApiAsync(
        string prompt,
        string apiKey,
        AIProviderRuntimeConfig config,
        AIUsageType usageType,
        string providerName,
        bool jsonMode = false)
    {
        var adapter = ResolveProviderAdapter(providerName);
        var invocation = await adapter.GenerateAsync(prompt, apiKey, config, jsonMode, CancellationToken.None);
        if (string.Equals(invocation.FinishReason, "length", StringComparison.OrdinalIgnoreCase)
            || string.Equals(invocation.FinishReason, "max_tokens", StringComparison.OrdinalIgnoreCase)
            || string.Equals(invocation.FinishReason, "MAX_TOKENS", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Response was truncated due to max_tokens limit. Consider increasing max_tokens or simplifying the prompt.");
        }

        await TryLogUsageAsync(
            usageType,
            providerName,
            config,
            invocation.InputTokens,
            invocation.OutputTokens,
            invocation.TotalTokens);

        return invocation.Content;
    }

    private IAIProviderAdapter ResolveProviderAdapter(string providerName)
    {
        var provider = string.IsNullOrWhiteSpace(providerName) ? "Groq" : providerName;
        var adapter = _providerAdapters.FirstOrDefault(x => x.CanHandle(provider));
        if (adapter != null)
        {
            return adapter;
        }

        var fallbackAdapter = _providerAdapters.FirstOrDefault(x => x.CanHandle("Groq"));
        if (fallbackAdapter != null)
        {
            return fallbackAdapter;
        }

        throw new InvalidOperationException($"No AI provider adapter registered for provider '{provider}'.");
    }

    private async Task TryLogUsageAsync(
        AIUsageType usageType,
        string providerName,
        AIProviderRuntimeConfig config,
        int inputTokens,
        int outputTokens,
        int totalTokens)
    {
        try
        {
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

    private static decimal CalculateCostUsd(AIProviderRuntimeConfig config, int inputTokens, int outputTokens)
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

    private static AIProviderRuntimeConfig AdjustConfigForAttempt(AIProviderRuntimeConfig baseConfig, int attempt)
    {
        return new AIProviderRuntimeConfig
        {
            Model = baseConfig.Model,
            MaxTokens = Math.Min(baseConfig.MaxTokens + (attempt * 1024), 24576),
            Temperature = Math.Min(baseConfig.Temperature + (attempt * 0.05f), 0.6f),
            RequestTimeoutSeconds = baseConfig.RequestTimeoutSeconds + (attempt * 15),
            InputCostPer1M = baseConfig.InputCostPer1M,
            OutputCostPer1M = baseConfig.OutputCostPer1M,
            BaseUrl = baseConfig.BaseUrl
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

}
