using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
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
    private readonly ILogger<AIVisionOcrService> _logger;

    public AIVisionOcrService(
        IApplicationDbContext dbContext,
        IEncryptionService encryptionService,
        IHttpClientFactory httpClientFactory,
        ILogger<AIVisionOcrService> logger)
    {
        _dbContext = dbContext;
        _encryptionService = encryptionService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string?> ExtractTextFromImageAsync(byte[] imageBytes)
    {
        try
        {
            _logger.LogInformation($"Extracting text using AI Vision ({imageBytes.Length} bytes)...");

            var aiConfig = await _dbContext.AIProviderConfigs
                .Where(c => c.UsageType == AIUsageType.DocumentExtraction && c.IsActive)
                .FirstOrDefaultAsync();

            if (aiConfig == null)
            {
                _logger.LogWarning("No AI config found for DocumentExtraction usage type");
                return null;
            }

            var apiKey = _encryptionService.Decrypt(aiConfig.EncryptedApiKey);

            var configJson = JsonDocument.Parse(aiConfig.ConfigJson);
            var model = configJson.RootElement.GetProperty("Model").GetString();
            var maxTokens = configJson.RootElement.GetProperty("MaxTokens").GetInt32();
            var temperature = configJson.RootElement.GetProperty("Temperature").GetDouble();

            var apiEndpoint = "https://api.groq.com/openai/v1/chat/completions";

            _logger.LogInformation($"Using provider: {aiConfig.ProviderName}, model: {model}");

            var base64Image = Convert.ToBase64String(imageBytes);
            var imageFormat = DetectImageFormat(imageBytes);
            var dataUrl = $"data:image/{imageFormat};base64,{base64Image}";

            var requestBody = new
            {
                model = model,
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
                temperature = temperature,
                max_tokens = maxTokens
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            _logger.LogInformation($"Calling {apiEndpoint}...");
            var response = await client.PostAsync(apiEndpoint, httpContent);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"AI Vision failed: {response.StatusCode} - {responseContent}");
                return null;
            }

            var jsonResponse = JsonDocument.Parse(responseContent);
            var extractedText = jsonResponse.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            _logger.LogInformation($"Extracted {extractedText?.Length ?? 0} characters");

            return string.IsNullOrWhiteSpace(extractedText) ? null : extractedText.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI Vision OCR failed");
            return null;
        }
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
