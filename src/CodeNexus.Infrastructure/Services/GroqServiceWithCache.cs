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
    private readonly IAIAccessPolicyService _aiAccessPolicyService;
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
        IAIAccessPolicyService aiAccessPolicyService,
        IEnumerable<IAIProviderAdapter>? providerAdapters = null)
    {
        _httpClient = httpClient;
        _context = context;
        _cacheService = cacheService;
        _encryptionService = encryptionService;
        _currentUserService = currentUserService;
        _subscriptionAccessService = subscriptionAccessService;
        _aiAccessPolicyService = aiAccessPolicyService;
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
            Guid selectedConfigId = Guid.Empty;
            try
            {
                var (apiKey, config, providerName, accessTier, userId, isMentor, configId) = await GetConfigAsync(usageType);
                selectedConfigId = configId;

                var adjustedConfig = attempt == 1 ? config : AdjustConfigForAttempt(config, attempt);

                var responseText = await CallProviderApiAsync(
                    prompt,
                    apiKey,
                    adjustedConfig,
                    usageType,
                    providerName,
                    accessTier,
                    userId,
                    isMentor,
                    jsonMode: true);
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
                if (ShouldRotateApiKey(ex))
                {
                    await TryRotateActiveConfigAsync(usageType, selectedConfigId, accessTier: null, CancellationToken.None);
                }
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

        var (apiKey, config, providerName, accessTier, userId, isMentor, configId) = await GetConfigAsync(usageType);
        try
        {
            return await CallProviderApiAsync(
                prompt,
                apiKey,
                config,
                usageType,
                providerName,
                accessTier,
                userId,
                isMentor,
                jsonMode: false);
        }
        catch (Exception ex) when (ShouldRotateApiKey(ex))
        {
            await TryRotateActiveConfigAsync(usageType, configId, accessTier, CancellationToken.None);
            var retry = await GetConfigAsync(usageType);
            return await CallProviderApiAsync(
                prompt,
                retry.apiKey,
                retry.config,
                usageType,
                retry.providerName,
                retry.accessTier,
                retry.userId,
                retry.isMentor,
                jsonMode: false);
        }
    }

    private async Task<(string apiKey, AIProviderRuntimeConfig config, string providerName, AIAccessTier accessTier, Guid userId, bool isMentor, Guid configId)> GetConfigAsync(AIUsageType usageType)
    {
        var accessResolution = await ResolveAccessResolutionAsync(CancellationToken.None);
        var selectedConfig = await ResolveConfigWithTierPreferenceAsync(usageType, accessResolution.PreferredTier);

        if (selectedConfig == null)
        {
            throw new InvalidOperationException($"AI configuration for {usageType} not found in database. Please configure it via AIConfig API.");
        }

        var decryptedApiKey = _encryptionService.Decrypt(selectedConfig.EncryptedApiKey);
        var cachedApiKey = await _cacheService.GetApiKeyAsync(usageType, selectedConfig.AccessTier, CancellationToken.None);
        var apiKey = cachedApiKey == decryptedApiKey ? cachedApiKey : decryptedApiKey;
        if (cachedApiKey != decryptedApiKey)
        {
            await _cacheService.SetApiKeyAsync(usageType, selectedConfig.AccessTier, decryptedApiKey, TimeSpan.FromHours(1));
        }

        if (accessResolution.ForceFreeDueToMentorLimit)
        {
            await TryCreateMentorDowngradeNotificationAsync(accessResolution.UserId, usageType, CancellationToken.None);
        }

        var config = ParseConfigJson(selectedConfig.ConfigJson);
        return (
            apiKey,
            config,
            string.IsNullOrWhiteSpace(selectedConfig.ProviderName) ? "Groq" : selectedConfig.ProviderName,
            selectedConfig.AccessTier,
            accessResolution.UserId,
            accessResolution.IsMentor,
            selectedConfig.ConfigId);
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

    private async Task<AccessResolution> ResolveAccessResolutionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = _currentUserService.GetUserId();
            var roleName = await _context.Users
                .AsNoTracking()
                .Where(u => u.UserId == userId)
                .Select(u => u.Role != null ? u.Role.RoleName : null)
                .FirstOrDefaultAsync(cancellationToken);

            if (IsPrivilegedRole(roleName))
            {
                return new AccessResolution(userId, false, true, AIAccessTier.Paid, false);
            }

            if (IsMentorRole(roleName))
            {
                var mentorLimit = await _aiAccessPolicyService.GetMentorPaidRequestsMonthlyLimitAsync(cancellationToken);
                if (mentorLimit <= 0)
                {
                    return new AccessResolution(userId, true, false, AIAccessTier.Paid, false);
                }

                var used = await CountMentorPaidAiUsageThisMonthAsync(userId, cancellationToken);
                if (used >= mentorLimit)
                {
                    return new AccessResolution(userId, true, false, AIAccessTier.Free, true);
                }

                return new AccessResolution(userId, true, false, AIAccessTier.Paid, false);
            }

            var canUsePaid = await _subscriptionAccessService.CanUsePaidModelsAsync(userId, cancellationToken);
            return new AccessResolution(userId, false, false, canUsePaid ? AIAccessTier.Paid : AIAccessTier.Free, false);
        }
        catch
        {
            return new AccessResolution(Guid.Empty, false, false, AIAccessTier.Free, false);
        }
    }

    private async Task<AIProviderConfig?> ResolveConfigByTierAsync(AIUsageType usageType, AIAccessTier tier)
    {
        return await _context.AIProviderConfigs
            .AsNoTracking()
            .Where(c => c.UsageType == usageType && c.AccessTier == tier && c.IsActive)
            .OrderByDescending(c => c.LastUpdated)
            .ThenBy(c => c.ConfigId)
            .FirstOrDefaultAsync(CancellationToken.None);
    }

    private async Task<AIProviderConfig?> ResolveAnyUsageConfigByTierAsync(AIAccessTier tier)
    {
        return await _context.AIProviderConfigs
            .AsNoTracking()
            .Where(c => c.AccessTier == tier && c.IsActive)
            .OrderBy(c => c.UsageType == AIUsageType.StructureGeneration ? 0 : 1)
            .ThenByDescending(c => c.LastUpdated)
            .ThenBy(c => c.ConfigId)
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

    private bool ShouldRotateApiKey(Exception ex)
    {
        var message = ex.Message.ToLowerInvariant();

        return message.Contains("401")
               || message.Contains("403")
               || message.Contains("402")
               || message.Contains("429")
               || message.Contains("unauthorized")
               || message.Contains("forbidden")
               || message.Contains("paymentrequired")
               || message.Contains("insufficient balance")
               || message.Contains("invalid api key")
               || message.Contains("too many requests")
               || message.Contains("timeout")
               || message.Contains("timed out")
               || message.Contains("task was canceled");
    }

    private async Task<bool> TryRotateActiveConfigAsync(
        AIUsageType usageType,
        Guid failedConfigId,
        AIAccessTier? accessTier,
        CancellationToken cancellationToken)
    {
        if (failedConfigId == Guid.Empty)
        {
            return false;
        }

        try
        {
            var failedConfig = await _context.AIProviderConfigs
                .FirstOrDefaultAsync(x => x.ConfigId == failedConfigId, cancellationToken);

            if (failedConfig == null)
            {
                return false;
            }

            var targetTier = accessTier ?? failedConfig.AccessTier;

            var candidates = await _context.AIProviderConfigs
                .Where(x => x.UsageType == usageType
                            && x.AccessTier == targetTier
                            && x.ConfigId != failedConfigId)
                .OrderByDescending(x => x.IsActive)
                .ThenByDescending(x => x.LastUpdated)
                .ToListAsync(cancellationToken);

            if (candidates.Count == 0)
            {
                return false;
            }

            var nextConfig = candidates[0];

            var group = await _context.AIProviderConfigs
                .Where(x => x.UsageType == usageType && x.AccessTier == targetTier)
                .ToListAsync(cancellationToken);

            foreach (var config in group)
            {
                config.IsActive = config.ConfigId == nextConfig.ConfigId;
                config.LastUpdated = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> CallProviderApiAsync(
        string prompt,
        string apiKey,
        AIProviderRuntimeConfig config,
        AIUsageType usageType,
        string providerName,
        AIAccessTier accessTier,
        Guid userId,
        bool isMentor,
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
            accessTier,
            userId,
            isMentor,
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
        AIAccessTier accessTier,
        Guid userId,
        bool isMentor,
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

            if (isMentor && accessTier == AIAccessTier.Paid && userId != Guid.Empty)
            {
                _context.FeatureUsageLogs.Add(new FeatureUsageLog
                {
                    FeatureUsageLogId = Guid.NewGuid(),
                    UserId = userId,
                    FeatureKey = SubscriptionFeatureKey.MentorPaidAiRequests,
                    CreatedAt = DateTime.UtcNow
                });
            }

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

    private async Task<int> CountMentorPaidAiUsageThisMonthAsync(Guid userId, CancellationToken cancellationToken)
    {
        var windowStartUtc = GetCurrentMonthStartUtc();
        return await _context.FeatureUsageLogs
            .AsNoTracking()
            .CountAsync(x =>
                x.UserId == userId
                && x.FeatureKey == SubscriptionFeatureKey.MentorPaidAiRequests
                && x.CreatedAt >= windowStartUtc, cancellationToken);
    }

    private async Task TryCreateMentorDowngradeNotificationAsync(Guid userId, AIUsageType usageType, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return;
        }

        try
        {
            var cooldownHours = await _aiAccessPolicyService.GetMentorDowngradeNotifyCooldownHoursAsync(cancellationToken);
            var cooldownBoundaryUtc = DateTime.UtcNow.AddHours(-cooldownHours);
            var title = "AI downgraded to free tier";

            var hasRecentNotification = await _context.Notifications
                .AsNoTracking()
                .AnyAsync(
                    n => n.UserId == userId
                         && n.Title == title
                         && n.CreatedAt >= cooldownBoundaryUtc,
                    cancellationToken);

            if (hasRecentNotification)
            {
                return;
            }

            await _context.Notifications.AddAsync(new Notification
            {
                NotificationId = Guid.NewGuid(),
                UserId = userId,
                Title = title,
                Message = $"Paid AI quota for this month has been reached. Requests for {usageType} are now served by free-tier model.",
                Type = NotificationType.Alert,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {

        }
    }

    private static bool IsMentorRole(string? roleName)
        => string.Equals(roleName, "Mentor", StringComparison.OrdinalIgnoreCase);

    private static bool IsPrivilegedRole(string? roleName)
        => string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase);

    private static DateTime GetCurrentMonthStartUtc()
    {
        var timezone = ResolveVietnamTimeZone();
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timezone);
        var startLocal = new DateTime(nowLocal.Year, nowLocal.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(startLocal, timezone);
    }

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
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

    private sealed record AccessResolution(
        Guid UserId,
        bool IsMentor,
        bool IsPrivilegedRole,
        AIAccessTier PreferredTier,
        bool ForceFreeDueToMentorLimit);

}
