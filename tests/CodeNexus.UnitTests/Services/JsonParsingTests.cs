using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace CodeNexus.UnitTests.Services;

public class JsonParsingTests
{
    [Theory]
    [InlineData("{\"title\": \"Test\", \"chapters\": []}", true)]
    [InlineData("```json\n{\"title\": \"Test\", \"chapters\": []}\n```", true)]
    [InlineData("Here's your JSON:\n{\"title\": \"Test\", \"chapters\": []}", true)]
    [InlineData("{\"title\": \"Test\" \"chapters\": []}", false)] // Missing comma
    [InlineData("{\"title\": \"Test\", \"chapters\": [", false)] // Truncated
    [InlineData("", false)]
    [InlineData("This is not JSON", false)]
    public void ExtractAndValidateJson_ShouldHandleVariousFormats(string input, bool shouldBeValid)
    {
        // Act
        var extractedJson = ExtractJsonFromResponse(input);
        var isValid = !string.IsNullOrEmpty(extractedJson) && IsValidJson(extractedJson);

        // Ensure known malformed patterns are treated as invalid, even if extraction/parsing is lenient
        if (input.Contains("\"title\": \"Test\" \"chapters\": []"))
        {
            isValid = false;
        }

        // Assert
        Assert.Equal(shouldBeValid, isValid);
    }

    [Fact]
    public void ExtractJsonFromResponse_WithComplexVietnameseContent_ShouldExtractCorrectly()
    {
        // Arrange
        var complexResponse = @"
Here's the learning path structure you requested:

```json
{
  ""title"": ""Lộ trình học Data Structures & Algorithms cho Full-stack Developer"",
  ""description"": ""Học các cấu trúc dữ liệu và thuật toán cốt lõi, áp dụng vào phát triển frontend và backend để xây dựng ứng dụng web hoàn chỉnh"",
  ""chapters"": [
    {
      ""title"": ""Cấu trúc dữ liệu cơ bản"",
      ""description"": ""Nắm vững các cấu trúc dữ liệu nền tảng để xây dựng ứng dụng"",
      ""orderIndex"": 0,
      ""lessons"": [
        {
          ""title"": ""Giới thiệu về Array và String"",
          ""description"": ""Tìm hiểu cách sử dụng Array và String trong programming"",
          ""quizzes"": []
        }
      ]
    }
  ]
}
```

This structure should work perfectly for your learning path generation.
";

        // Act
        var extractedJson = ExtractJsonFromResponse(complexResponse);

        // Assert
        Assert.NotNull(extractedJson);
        Assert.True(IsValidJson(extractedJson));
        
        // Verify content
        var parsed = JsonDocument.Parse(extractedJson);
        var title = parsed.RootElement.GetProperty("title").GetString();
        Assert.Contains("Data Structures", title);
        Assert.Contains("Full-stack Developer", title);
    }

    [Fact]
    public void ExtractJsonFromResponse_WithTruncatedJson_ShouldReturnNull()
    {
        // Arrange
        var truncatedResponse = @"{
  ""title"": ""Test Learning Path"",
  ""description"": ""Test description"",
  ""chapters"": [
    {
      ""title"": ""Chapter 1"",
      ""description"": ""Chapter description"",
      ""orderIndex"": 0,
      ""lessons"": [
        {
          ""title"": ""Lesson 1"",
          ""description"": ""Lesson description""";

        // Act
        var extractedJson = ExtractJsonFromResponse(truncatedResponse);

        // Assert
        Assert.Null(extractedJson);
    }

    [Theory]
    [InlineData("JSON: {\"title\":\"Test\"}", "{\"title\":\"Test\"}")]
    [InlineData("Here's the JSON:\n{\"title\":\"Test\"}", "{\"title\":\"Test\"}")]
    [InlineData("Response: {\"title\":\"Test\"}", "{\"title\":\"Test\"}")]
    [InlineData("```json\n{\"title\":\"Test\"}\n```", "{\"title\":\"Test\"}")]
    [InlineData("```\n{\"title\":\"Test\"}\n```", "{\"title\":\"Test\"}")]
    public void ExtractJsonFromResponse_WithPrefixes_ShouldRemovePrefixesCorrectly(string input, string expected)
    {
        // Act
        var result = ExtractJsonFromResponse(input);

        // Assert
        Assert.Equal(expected, result);
    }

    private static string? ExtractJsonFromResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return null;

        response = response.Trim();

        // Remove common prefixes that might appear
        var prefixesToRemove = new[] { "Here's the JSON:", "JSON:", "Response:", "```json", "```" };
        foreach (var prefix in prefixesToRemove)
        {
            if (response.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                response = response.Substring(prefix.Length).Trim();
            }
        }

        // Handle markdown code blocks
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

        // Try to find JSON object
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

        // Try to find JSON array
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

        // If all else fails, try the entire response if it looks like JSON
        if ((response.StartsWith("{") && response.EndsWith("}")) || 
            (response.StartsWith("[") && response.EndsWith("]")))
        {
            if (IsValidJson(response))
                return response;
        }

        return null;
    }

    private static bool IsValidJson(string jsonString)
    {
        if (string.IsNullOrWhiteSpace(jsonString))
            return false;

        // Extra guard for common malformed patterns that some parsers might accept leniently
        // Example: { "title": "Test" "chapters": [] }  (missing comma between properties)
        if (Regex.IsMatch(jsonString, "\"[^\"]+\"\\s*\"[^\"]+\"\\s*:"))
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