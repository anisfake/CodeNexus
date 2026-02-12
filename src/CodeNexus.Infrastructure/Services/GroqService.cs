using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Infrastructure.Settings;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace CodeNexus.Infrastructure.Services
{
    public class GroqService : IAIGeneratorService
    {
        private readonly GroqSettings _settings;
        private readonly HttpClient _httpClient;
        private const string GroqApiUrl = "https://api.groq.com/openai/v1/chat/completions";

        public GroqService(IOptions<GroqSettings> options, HttpClient httpClient)
        {
            _settings = options.Value;
            _httpClient = httpClient;

            if (string.IsNullOrEmpty(_settings.ApiKey))
                throw new InvalidOperationException("Groq API Key is not configured");

            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_settings.ApiKey}");
        }

        public async Task<T> GenerateStructureAsync<T>(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt cannot be empty", nameof(prompt));

            var responseText = await CallGroqApiAsync(prompt, jsonMode: true);
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

        public async Task<string> GenerateContentAsync(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt cannot be empty", nameof(prompt));

            return await CallGroqApiAsync(prompt, jsonMode: false);
        }

        private async Task<string> CallGroqApiAsync(string prompt, bool jsonMode = false)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_settings.RequestTimeoutSeconds));

            var messages = new List<object>();

            if (jsonMode)
            {
                messages.Add(new { role = "system", content = "You are a helpful assistant. You must respond with valid JSON only. No markdown, no extra text." });
            }

            messages.Add(new { role = "user", content = prompt });

            var requestBody = new Dictionary<string, object>
            {
                ["model"] = _settings.Model,
                ["messages"] = messages,
                ["max_tokens"] = _settings.MaxTokens,
                ["temperature"] = _settings.Temperature
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

            var response = await _httpClient.PostAsync(GroqApiUrl, jsonContent, cts.Token);

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
            var jsonEnd = response.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
                return response.Substring(jsonStart, jsonEnd - jsonStart + 1).Trim();

            var arrayStart = response.IndexOf('[');
            var arrayEnd = response.LastIndexOf(']');
            if (arrayStart >= 0 && arrayEnd > arrayStart)
                return response.Substring(arrayStart, arrayEnd - arrayStart + 1).Trim();

            return null;
        }
    }
}
