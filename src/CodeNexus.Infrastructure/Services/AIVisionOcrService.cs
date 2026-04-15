using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Domain.Enums;
using CodeNexus.Infrastructure.Services.AIProviders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CodeNexus.Infrastructure.Services;

public class AIVisionOcrService : IOcrService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IEncryptionService _encryptionService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<AIVisionOcrService> _logger;

    public AIVisionOcrService(
        IApplicationDbContext dbContext,
        IEncryptionService encryptionService,
        IHttpClientFactory httpClientFactory,
        ICurrentUserService currentUserService,
        ILogger<AIVisionOcrService> logger)
    {
        _dbContext = dbContext;
        _encryptionService = encryptionService;
        _httpClientFactory = httpClientFactory;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<string?> ExtractTextFromImageAsync(byte[] imageBytes)
    {
        try
        {
            _logger.LogInformation($"Extracting text using AI Vision ({imageBytes.Length} bytes)...");

            var aiConfig = await ResolveActiveConfigAsync();

            if (aiConfig == null)
            {
                _logger.LogWarning("No AI config found for OCR. Configure DocumentExtraction usage type (preferred) or Assistant (legacy fallback).");
                return null;
            }

            var apiKey = _encryptionService.Decrypt(aiConfig.EncryptedApiKey);
            var runtimeConfig = ParseRuntimeConfig(aiConfig.ConfigJson, aiConfig.ProviderName);
            var providerName = string.IsNullOrWhiteSpace(aiConfig.ProviderName) ? "Groq" : aiConfig.ProviderName;

            _logger.LogInformation("Using provider: {Provider}, model: {Model}, usage type: {UsageType}, access tier: {AccessTier}",
                providerName, runtimeConfig.Model, aiConfig.UsageType, aiConfig.AccessTier);

            string? extractedText;
            if (providerName.Contains("mistral", StringComparison.OrdinalIgnoreCase))
            {
                extractedText = await ExtractWithMistralOcrAsync(imageBytes, apiKey, runtimeConfig);
            }
            else
            {
                extractedText = await ExtractWithOpenAiCompatibleVisionAsync(imageBytes, apiKey, runtimeConfig, providerName);
            }

            _logger.LogInformation("Extracted {Length} characters", extractedText?.Length ?? 0);
            return string.IsNullOrWhiteSpace(extractedText) ? null : extractedText.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI Vision OCR failed");
            return null;
        }
    }

    private async Task<string?> ExtractWithOpenAiCompatibleVisionAsync(
        byte[] imageBytes,
        string apiKey,
        AIProviderRuntimeConfig runtimeConfig,
        string providerName)
    {
        var base64Image = Convert.ToBase64String(imageBytes);
        var imageFormat = DetectImageFormat(imageBytes);
        var dataUrl = $"data:image/{imageFormat};base64,{base64Image}";

        var requestBody = new
        {
            model = runtimeConfig.Model,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "text",
                            text = "Extract ALL text from this image in the exact order it appears visually (top to bottom, left to right). Include all text you see. Return ONLY the extracted text, no explanations."
                        },
                        new
                        {
                            type = "image_url",
                            image_url = new
                            {
                                url = dataUrl
                            }
                        }
                    }
                }
            },
            temperature = runtimeConfig.Temperature,
            max_tokens = runtimeConfig.MaxTokens
        };

        var endpoint = ResolveVisionEndpoint(providerName, runtimeConfig.BaseUrl);
        using var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        var jsonContent = JsonSerializer.Serialize(requestBody);
        using var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        _logger.LogInformation("Calling OCR-compatible vision endpoint: {Endpoint}", endpoint);
        var response = await client.PostAsync(endpoint, httpContent);
        var responseContent = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("AI Vision chat endpoint failed: {StatusCode} - {Response}", response.StatusCode, responseContent);
            return null;
        }

        using var jsonResponse = JsonDocument.Parse(responseContent);
        var extractedText = ExtractContentFromChatResponse(jsonResponse.RootElement);
        return extractedText;
    }

    private async Task<string?> ExtractWithMistralOcrAsync(
        byte[] imageBytes,
        string apiKey,
        AIProviderRuntimeConfig runtimeConfig)
    {
        var base64Image = Convert.ToBase64String(imageBytes);
        var imageFormat = DetectImageFormat(imageBytes);
        var dataUrl = $"data:image/{imageFormat};base64,{base64Image}";

        var model = string.IsNullOrWhiteSpace(runtimeConfig.Model)
            ? "mistral-ocr-latest"
            : runtimeConfig.Model;

        var requestBody = new
        {
            model,
            document = new
            {
                type = "image_url",
                image_url = dataUrl
            }
        };

        var endpoint = ResolveMistralOcrEndpoint(runtimeConfig.BaseUrl);
        using var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        var jsonContent = JsonSerializer.Serialize(requestBody);
        using var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        _logger.LogInformation("Calling Mistral OCR endpoint: {Endpoint}", endpoint);
        var response = await client.PostAsync(endpoint, httpContent);
        var responseContent = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Mistral OCR endpoint failed: {StatusCode} - {Response}", response.StatusCode, responseContent);
            return null;
        }

        using var jsonResponse = JsonDocument.Parse(responseContent);
        if (!jsonResponse.RootElement.TryGetProperty("pages", out var pages) ||
            pages.ValueKind != JsonValueKind.Array ||
            pages.GetArrayLength() == 0)
        {
            return null;
        }

        var markdowns = new List<string>();
        foreach (var page in pages.EnumerateArray())
        {
            if (page.TryGetProperty("markdown", out var markdownElement) &&
                markdownElement.ValueKind == JsonValueKind.String)
            {
                var markdown = markdownElement.GetString();
                if (!string.IsNullOrWhiteSpace(markdown))
                {
                    markdowns.Add(markdown);
                }
            }
        }

        return markdowns.Count == 0 ? null : string.Join("\n\n", markdowns);
    }

    private async Task<CodeNexus.Domain.Entities.AIProviderConfig?> ResolveActiveConfigAsync()
    {
        var preferredTier = await ResolvePreferredTierAsync();
        var fallbackTier = preferredTier == AIAccessTier.Paid ? AIAccessTier.Free : AIAccessTier.Paid;

        var config = await FindConfigAsync(AIUsageType.DocumentExtraction, preferredTier);
        if (config != null) return config;

        config = await FindConfigAsync(AIUsageType.DocumentExtraction, fallbackTier);
        if (config != null) return config;

        config = await FindConfigAsync(AIUsageType.Assistant, preferredTier);
        if (config != null)
        {
            _logger.LogWarning("DocumentExtraction config not found. Falling back to Assistant config for OCR.");
            return config;
        }

        config = await FindConfigAsync(AIUsageType.Assistant, fallbackTier);
        if (config != null)
        {
            _logger.LogWarning("DocumentExtraction config not found. Falling back to Assistant config in secondary tier for OCR.");
            return config;
        }

        return null;
    }

    private async Task<CodeNexus.Domain.Entities.AIProviderConfig?> FindConfigAsync(AIUsageType usageType, AIAccessTier tier)
    {
        return await _dbContext.AIProviderConfigs
            .AsNoTracking()
            .Where(c => c.UsageType == usageType && c.AccessTier == tier && c.IsActive)
            .OrderByDescending(c => c.LastUpdated)
            .ThenBy(c => c.ConfigId)
            .FirstOrDefaultAsync();
    }

    private async Task<AIAccessTier> ResolvePreferredTierAsync()
    {
        try
        {
            var userId = _currentUserService.GetUserId();
            var userAccess = await _dbContext.Users
                .AsNoTracking()
                .Where(u => u.UserId == userId)
                .Select(u => new
                {
                    RoleName = u.Role != null ? u.Role.RoleName : string.Empty,
                    u.BalanceVnd
                })
                .FirstOrDefaultAsync();

            if (string.Equals(userAccess?.RoleName, "Admin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(userAccess?.RoleName, "Mentor", StringComparison.OrdinalIgnoreCase))
            {
                return AIAccessTier.Paid;
            }

            return (userAccess?.BalanceVnd ?? 0m) > 0m ? AIAccessTier.Paid : AIAccessTier.Free;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve user tier for OCR. Falling back to free tier config.");
            return AIAccessTier.Free;
        }
    }

    private AIProviderRuntimeConfig ParseRuntimeConfig(string? configJson, string? providerName)
    {
        var fallbackModel = providerName != null && providerName.Contains("mistral", StringComparison.OrdinalIgnoreCase)
            ? "mistral-ocr-latest"
            : "meta-llama/llama-4-scout-17b-16e-instruct";

        var fallback = new AIProviderRuntimeConfig
        {
            Model = fallbackModel,
            MaxTokens = 8192,
            Temperature = 0.2f,
            RequestTimeoutSeconds = 120
        };

        if (string.IsNullOrWhiteSpace(configJson))
        {
            return fallback;
        }

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var parsed = JsonSerializer.Deserialize<AIProviderRuntimeConfig>(configJson, options);
            if (parsed == null)
            {
                return fallback;
            }

            if (string.IsNullOrWhiteSpace(parsed.Model))
            {
                parsed.Model = fallback.Model;
            }

            if (parsed.MaxTokens <= 0)
            {
                parsed.MaxTokens = fallback.MaxTokens;
            }

            if (parsed.RequestTimeoutSeconds <= 0)
            {
                parsed.RequestTimeoutSeconds = fallback.RequestTimeoutSeconds;
            }

            return parsed;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse OCR AI runtime config. Using fallback defaults.");
            return fallback;
        }
    }

    private static string ResolveVisionEndpoint(string providerName, string? baseUrl)
    {
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            return baseUrl;
        }

        if (providerName.Contains("openai", StringComparison.OrdinalIgnoreCase))
        {
            return "https://api.openai.com/v1/chat/completions";
        }

        if (providerName.Contains("mistral", StringComparison.OrdinalIgnoreCase))
        {
            return "https://api.mistral.ai/v1/chat/completions";
        }

        return "https://api.groq.com/openai/v1/chat/completions";
    }

    private static string ResolveMistralOcrEndpoint(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return "https://api.mistral.ai/v1/ocr";
        }

        var normalized = baseUrl.TrimEnd('/');

        if (normalized.EndsWith("/v1/ocr", StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        if (normalized.EndsWith("/v1/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            return normalized[..^"/v1/chat/completions".Length] + "/v1/ocr";
        }

        if (normalized.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            return normalized[..^"/chat/completions".Length] + "/ocr";
        }

        if (normalized.Contains("/v1", StringComparison.OrdinalIgnoreCase))
        {
            return normalized + "/ocr";
        }

        return normalized + "/v1/ocr";
    }

    private static string? ExtractContentFromChatResponse(JsonElement root)
    {
        if (!root.TryGetProperty("choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array ||
            choices.GetArrayLength() == 0)
        {
            return null;
        }

        var message = choices[0].GetProperty("message");
        if (!message.TryGetProperty("content", out var content))
        {
            return null;
        }

        if (content.ValueKind == JsonValueKind.String)
        {
            return content.GetString();
        }

        if (content.ValueKind == JsonValueKind.Array)
        {
            var textParts = new List<string>();
            foreach (var part in content.EnumerateArray())
            {
                if (part.ValueKind == JsonValueKind.Object &&
                    part.TryGetProperty("type", out var typeElement) &&
                    typeElement.ValueKind == JsonValueKind.String &&
                    string.Equals(typeElement.GetString(), "text", StringComparison.OrdinalIgnoreCase) &&
                    part.TryGetProperty("text", out var textElement) &&
                    textElement.ValueKind == JsonValueKind.String)
                {
                    var text = textElement.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        textParts.Add(text);
                    }
                }
            }

            if (textParts.Count > 0)
            {
                return string.Join("\n", textParts);
            }
        }

        return null;
    }

    private string DetectImageFormat(byte[] imageBytes)
    {
        if (imageBytes.Length < 4) return "jpeg";

        if (imageBytes[0] == 0x89 && imageBytes[1] == 0x50 && imageBytes[2] == 0x4E && imageBytes[3] == 0x47)
            return "png";

        if (imageBytes[0] == 0xFF && imageBytes[1] == 0xD8)
            return "jpeg";

        return "jpeg";
    }
}
