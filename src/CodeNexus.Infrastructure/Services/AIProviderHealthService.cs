using System.Globalization;
using System.Text.Json;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.AIConfigs.DTOs;
using CodeNexus.Infrastructure.Services.AIProviders;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Infrastructure.Services;

public class AIProviderHealthService : IAIProviderHealthService
{
    private readonly IReadOnlyCollection<IAIProviderAdapter> _providerAdapters;
    private readonly IApplicationDbContext _context;
    private readonly IEncryptionService _encryptionService;

    public AIProviderHealthService(
        IEnumerable<IAIProviderAdapter> providerAdapters,
        IApplicationDbContext context,
        IEncryptionService encryptionService)
    {
        _providerAdapters = providerAdapters.ToList();
        _context = context;
        _encryptionService = encryptionService;
    }

    public async Task<Result<TestAIProviderResponse>> TestApiKeyAsync(
        TestAIProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ProviderName))
        {
            return Result<TestAIProviderResponse>.Failure("PROVIDER_REQUIRED", "Provider name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            return Result<TestAIProviderResponse>.Failure("API_KEY_REQUIRED", "API key is required.");
        }

        var adapter = _providerAdapters.FirstOrDefault(x => x.CanHandle(request.ProviderName));
        if (adapter == null)
        {
            return Result<TestAIProviderResponse>.Failure(
                "UNSUPPORTED_PROVIDER",
                $"Provider '{request.ProviderName}' is not supported.");
        }

        var runtimeConfig = BuildRuntimeConfig(request.ProviderName, request.ConfigJson);

        var prompt = request.JsonMode
            ? "Return ONLY this JSON object: {\"ok\":true}"
            : "Reply with exactly this text: OK";

        try
        {
            var invocation = await adapter.GenerateAsync(
                prompt,
                request.ApiKey.Trim(),
                runtimeConfig,
                request.JsonMode,
                cancellationToken);

            var preview = NormalizePreview(invocation.Content);

            return Result<TestAIProviderResponse>.Success(new TestAIProviderResponse(
                IsValid: true,
                ProviderName: request.ProviderName,
                Model: runtimeConfig.Model,
                ResponsePreview: preview,
                InputTokens: invocation.InputTokens,
                OutputTokens: invocation.OutputTokens,
                TotalTokens: invocation.TotalTokens
            ));
        }
        catch (Exception ex)
        {
            var message = ex.Message;
            if (message.Length > 800)
            {
                message = message[..800];
            }

            return Result<TestAIProviderResponse>.Failure("AI_PROVIDER_TEST_FAILED", message);
        }
    }

    public async Task<Result<List<TestStoredAIProviderResponse>>> TestStoredApiKeysAsync(
        TestStoredAIProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AIProviderConfigs.AsNoTracking();

        if (request.OnlyActive)
        {
            query = query.Where(x => x.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(request.ProviderName))
        {
            query = query.Where(x => x.ProviderName.Contains(request.ProviderName));
        }

        var configs = await query
            .OrderByDescending(x => x.LastUpdated)
            .ToListAsync(cancellationToken);

        if (configs.Count == 0)
        {
            return Result<List<TestStoredAIProviderResponse>>.Failure(
                "CONFIG_NOT_FOUND",
                "No AI config found in database with current filter.");
        }

        var responses = new List<TestStoredAIProviderResponse>();

        foreach (var config in configs)
        {
            responses.Add(await TestSingleStoredConfigAsync(config, cancellationToken));
        }

        return Result<List<TestStoredAIProviderResponse>>.Success(responses);
    }

    public async Task<Result<TestStoredAIProviderResponse>> TestStoredApiKeyByConfigIdAsync(
        Guid configId,
        CancellationToken cancellationToken = default)
    {
        var config = await _context.AIProviderConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ConfigId == configId, cancellationToken);

        if (config == null)
        {
            return Result<TestStoredAIProviderResponse>.Failure(
                "CONFIG_NOT_FOUND",
                "AI config not found.");
        }

        var result = await TestSingleStoredConfigAsync(config, cancellationToken);
        return Result<TestStoredAIProviderResponse>.Success(result);
    }

    public async Task<Result<List<StoredAIProviderKeyResponse>>> GetStoredApiKeysAsync(
        CancellationToken cancellationToken = default)
    {
        var configs = await _context.AIProviderConfigs
            .AsNoTracking()
            .OrderBy(x => x.ProviderName)
            .ThenBy(x => x.UsageType)
            .ThenBy(x => x.AccessTier)
            .ThenByDescending(x => x.LastUpdated)
            .ToListAsync(cancellationToken);

        if (configs.Count == 0)
        {
            return Result<List<StoredAIProviderKeyResponse>>.Failure(
                "CONFIG_NOT_FOUND",
                "No AI config found in database.");
        }

        var responses = configs.Select(BuildStoredKeyResponse).ToList();
        return Result<List<StoredAIProviderKeyResponse>>.Success(responses);
    }

    private static AIProviderRuntimeConfig BuildRuntimeConfig(
        string providerName,
        Dictionary<string, object>? configJson)
    {
        var config = new AIProviderRuntimeConfig
        {
            Model = ResolveDefaultModel(providerName),
            MaxTokens = 64,
            Temperature = 0.0f,
            RequestTimeoutSeconds = 30
        };

        if (configJson == null || configJson.Count == 0)
        {
            return config;
        }

        if (TryGetValue(configJson, "model", out var model) && !string.IsNullOrWhiteSpace(model))
        {
            config.Model = model;
        }

        if (TryGetValue(configJson, "baseUrl", out var baseUrl) && !string.IsNullOrWhiteSpace(baseUrl))
        {
            config.BaseUrl = baseUrl;
        }

        if (TryGetValue(configJson, "maxTokens", out var maxTokens) && int.TryParse(maxTokens, out var parsedMaxTokens))
        {
            config.MaxTokens = Clamp(parsedMaxTokens, 16, 512);
        }

        if (TryGetValue(configJson, "temperature", out var temperature)
            && float.TryParse(temperature, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedTemperature))
        {
            config.Temperature = Clamp(parsedTemperature, 0f, 1f);
        }

        if (TryGetValue(configJson, "requestTimeoutSeconds", out var timeout)
            && int.TryParse(timeout, out var parsedTimeout))
        {
            config.RequestTimeoutSeconds = Clamp(parsedTimeout, 5, 120);
        }

        return config;
    }

    private static string ResolveDefaultModel(string providerName)
    {
        if (providerName.Contains("mistral", StringComparison.OrdinalIgnoreCase))
        {
            return "mistral-small-latest";
        }

        if (providerName.Contains("gemini", StringComparison.OrdinalIgnoreCase)
            || providerName.Contains("google", StringComparison.OrdinalIgnoreCase))
        {
            return "gemini-1.5-flash";
        }

        return "meta-llama/llama-4-scout-17b-16e-instruct";
    }

    private static string NormalizePreview(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var compact = content.Replace("\r", " ").Replace("\n", " ").Trim();
        if (compact.Length > 180)
        {
            compact = compact[..180] + "...";
        }

        return compact;
    }

    private static bool TryGetValue(Dictionary<string, object> source, string key, out string value)
    {
        var kv = source.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
        if (kv.Key == null)
        {
            value = string.Empty;
            return false;
        }

        value = ConvertToString(kv.Value);
        return !string.IsNullOrWhiteSpace(value);
    }

    private static string ConvertToString(object? value)
    {
        if (value == null)
        {
            return string.Empty;
        }

        if (value is string text)
        {
            return text;
        }

        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Number => element.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => string.Empty,
                _ => element.GetRawText()
            };
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private async Task<TestStoredAIProviderResponse> TestSingleStoredConfigAsync(
        Domain.Entities.AIProviderConfig config,
        CancellationToken cancellationToken)
    {
        string apiKey;
        try
        {
            apiKey = _encryptionService.Decrypt(config.EncryptedApiKey);
        }
        catch (Exception ex)
        {
            return BuildStoredFailure(config, ResolveModelFromConfig(config), "API_KEY_DECRYPT_FAILED", ex.Message);
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return BuildStoredFailure(config, ResolveModelFromConfig(config), "API_KEY_EMPTY", "Stored API key is empty.");
        }

        var request = new TestAIProviderRequest(
            ProviderName: config.ProviderName,
            ApiKey: apiKey,
            ConfigJson: ParseConfigJson(config.ConfigJson),
            JsonMode: false);

        var testResult = await TestApiKeyAsync(request, cancellationToken);

        if (!testResult.IsSuccess || testResult.Value is null)
        {
            return BuildStoredFailure(
                config,
                ResolveModelFromConfig(config),
                testResult.ErrorCode ?? "AI_PROVIDER_TEST_FAILED",
                testResult.ErrorMessage ?? "Failed to test provider.");
        }

        return new TestStoredAIProviderResponse(
            ConfigId: config.ConfigId,
            ProviderName: config.ProviderName,
            UsageType: config.UsageType,
            AccessTier: config.AccessTier,
            IsActive: config.IsActive,
            IsValid: testResult.Value.IsValid,
            Model: testResult.Value.Model,
            ResponsePreview: testResult.Value.ResponsePreview,
            InputTokens: testResult.Value.InputTokens,
            OutputTokens: testResult.Value.OutputTokens,
            TotalTokens: testResult.Value.TotalTokens,
            ErrorCode: null,
            ErrorMessage: null);
    }

    private static TestStoredAIProviderResponse BuildStoredFailure(
        Domain.Entities.AIProviderConfig config,
        string model,
        string errorCode,
        string errorMessage)
    {
        var safeMessage = errorMessage;
        if (safeMessage.Length > 800)
        {
            safeMessage = safeMessage[..800];
        }

        return new TestStoredAIProviderResponse(
            ConfigId: config.ConfigId,
            ProviderName: config.ProviderName,
            UsageType: config.UsageType,
            AccessTier: config.AccessTier,
            IsActive: config.IsActive,
            IsValid: false,
            Model: model,
            ResponsePreview: string.Empty,
            InputTokens: 0,
            OutputTokens: 0,
            TotalTokens: 0,
            ErrorCode: errorCode,
            ErrorMessage: safeMessage);
    }

    private static Dictionary<string, object> ParseConfigJson(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
        {
            return new Dictionary<string, object>();
        }

        try
        {
            using var document = JsonDocument.Parse(configJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new Dictionary<string, object>();
            }

            return ConvertObject(document.RootElement);
        }
        catch
        {
            return new Dictionary<string, object>();
        }
    }

    private static Dictionary<string, object> ConvertObject(JsonElement element)
    {
        var result = new Dictionary<string, object>();
        foreach (var property in element.EnumerateObject())
        {
            var converted = ConvertJsonValue(property.Value);
            if (converted is not null)
            {
                result[property.Name] = converted;
            }
        }

        return result;
    }

    private static object? ConvertJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => ConvertObject(element),
            JsonValueKind.Array => element.EnumerateArray()
                .Select(ConvertJsonValue)
                .Where(x => x is not null)
                .Cast<object>()
                .ToList(),
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number when element.TryGetDouble(out var doubleValue) => doubleValue,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static string ResolveModelFromConfig(Domain.Entities.AIProviderConfig config)
    {
        var parsed = ParseConfigJson(config.ConfigJson);
        return TryGetValue(parsed, "model", out var model) && !string.IsNullOrWhiteSpace(model)
            ? model
            : ResolveDefaultModel(config.ProviderName);
    }

    private StoredAIProviderKeyResponse BuildStoredKeyResponse(Domain.Entities.AIProviderConfig config)
    {
        var model = ResolveModelFromConfig(config);

        try
        {
            var apiKey = _encryptionService.Decrypt(config.EncryptedApiKey);
            return new StoredAIProviderKeyResponse(
                ConfigId: config.ConfigId,
                ProviderName: config.ProviderName,
                UsageType: config.UsageType,
                AccessTier: config.AccessTier,
                IsActive: config.IsActive,
                LastUpdated: config.LastUpdated,
                ApiKey: apiKey,
                MaskedApiKey: MaskApiKey(apiKey),
                Model: model,
                ReadError: null);
        }
        catch (Exception ex)
        {
            return new StoredAIProviderKeyResponse(
                ConfigId: config.ConfigId,
                ProviderName: config.ProviderName,
                UsageType: config.UsageType,
                AccessTier: config.AccessTier,
                IsActive: config.IsActive,
                LastUpdated: config.LastUpdated,
                ApiKey: string.Empty,
                MaskedApiKey: "****",
                Model: model,
                ReadError: ex.Message.Length > 300 ? ex.Message[..300] : ex.Message);
        }
    }

    private static string MaskApiKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return string.Empty;
        }

        if (apiKey.Length <= 8)
        {
            return new string('*', apiKey.Length);
        }

        var prefix = apiKey[..4];
        var suffix = apiKey[^4..];
        return $"{prefix}...{suffix}";
    }

    private static int Clamp(int value, int min, int max)
        => value < min ? min : value > max ? max : value;

    private static float Clamp(float value, float min, float max)
        => value < min ? min : value > max ? max : value;
}
