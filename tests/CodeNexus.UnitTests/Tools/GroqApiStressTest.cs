using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Domain.Enums;
using CodeNexus.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace CodeNexus.UnitTests.Tools;

/// <summary>
/// Stress test tool để test JSON parsing với Groq API thực tế
/// Chỉ chạy khi có API key thực và muốn test integration
/// </summary>
public class GroqApiStressTest
{
    private readonly ITestOutputHelper _output;

    public GroqApiStressTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact(Skip = "Integration test - chỉ chạy khi cần test thực tế với API")]
    public async Task StressTest_GenerateLearningPath_MultipleAttempts()
    {
        // Arrange
        var httpClient = new HttpClient();
        var mockContext = new Mock<IApplicationDbContext>();
        var mockCacheService = new Mock<IAIConfigCacheService>();
        var mockEncryptionService = new Mock<IEncryptionService>();

        // Setup cache service to return API key (cần thay bằng API key thực)
        mockCacheService
            .Setup(x => x.GetApiKeyAsync(It.IsAny<AIUsageType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("your-actual-groq-api-key-here");

        var service = new GroqServiceWithCache(httpClient, mockContext.Object, mockCacheService.Object, mockEncryptionService.Object);

        var testPrompts = CreateComplexVietnamesePrompt()
            .Concat(CreateSimpleEnglishPrompt())
            .Concat(CreateVeryComplexPrompt())
            .Concat(CreateEdgeCasePrompt())
            .ToArray();

        var results = new List<TestResult>();

        // Act
        foreach (var (prompt, testName) in testPrompts)
        {
            _output.WriteLine($"Testing: {testName}");
            
            for (int attempt = 1; attempt <= 5; attempt++)
            {
                try
                {
                    var startTime = DateTime.UtcNow;
                    var result = await service.GenerateStructureAsync<LearningPathTestDto>(prompt);
                    var duration = DateTime.UtcNow - startTime;

                    results.Add(new TestResult
                    {
                        TestName = testName,
                        Attempt = attempt,
                        Success = true,
                        Duration = duration,
                        ChapterCount = result.Chapters?.Count ?? 0,
                        ErrorMessage = null
                    });

                    _output.WriteLine($"  Attempt {attempt}: SUCCESS ({duration.TotalMilliseconds:F0}ms, {result.Chapters?.Count ?? 0} chapters)");
                }
                catch (Exception ex)
                {
                    results.Add(new TestResult
                    {
                        TestName = testName,
                        Attempt = attempt,
                        Success = false,
                        Duration = TimeSpan.Zero,
                        ChapterCount = 0,
                        ErrorMessage = ex.Message
                    });

                    _output.WriteLine($"  Attempt {attempt}: FAILED - {ex.Message}");
                }

                // Delay between attempts to avoid rate limiting
                await Task.Delay(2000);
            }
        }

        // Assert & Report
        var successRate = results.Count(r => r.Success) / (double)results.Count * 100;
        var avgDuration = results.Where(r => r.Success).Average(r => r.Duration.TotalMilliseconds);

        _output.WriteLine($"\n=== STRESS TEST RESULTS ===");
        _output.WriteLine($"Total tests: {results.Count}");
        _output.WriteLine($"Success rate: {successRate:F1}%");
        _output.WriteLine($"Average duration: {avgDuration:F0}ms");

        var failedTests = results.Where(r => !r.Success).ToList();
        if (failedTests.Any())
        {
            _output.WriteLine($"\nFailed tests: {failedTests.Count}");
            foreach (var failed in failedTests)
            {
                _output.WriteLine($"  {failed.TestName} (Attempt {failed.Attempt}): {failed.ErrorMessage}");
            }
        }

        // Test should pass if success rate is above 80%
        Assert.True(successRate >= 80, $"Success rate {successRate:F1}% is below acceptable threshold of 80%");
    }

    [Fact(Skip = "Manual test - để test các edge case JSON parsing")]
    public async Task Test_JsonParsingEdgeCases()
    {
        var testCases = new[]
        {
            ("Valid JSON", "{\"title\": \"Test\", \"chapters\": []}"),
            ("JSON with markdown", "```json\n{\"title\": \"Test\", \"chapters\": []}\n```"),
            ("JSON with prefix", "Here's your JSON:\n{\"title\": \"Test\", \"chapters\": []}"),
            ("Truncated JSON", "{\"title\": \"Test\", \"chapters\": [{\"title\": \"Ch1\""),
            ("Invalid JSON", "{\"title\": \"Test\" \"chapters\": []}"), // Missing comma
            ("Empty response", ""),
            ("Non-JSON response", "This is not JSON at all"),
        };

        foreach (var (testName, jsonContent) in testCases)
        {
            _output.WriteLine($"Testing: {testName}");
            
            try
            {
                // Test the JSON extraction logic directly
                var extractedJson = TestJsonExtraction(jsonContent);
                _output.WriteLine($"  Extracted: {extractedJson ?? "NULL"}");
                
                if (!string.IsNullOrEmpty(extractedJson))
                {
                    var isValid = TestJsonValidation(extractedJson);
                    _output.WriteLine($"  Valid: {isValid}");
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"  Error: {ex.Message}");
            }
        }
    }

    private static (string prompt, string testName)[] CreateComplexVietnamesePrompt()
    {
        return new[]
        {
            ("""
            Generate a complete learning path structure in valid JSON format.

            === CONTEXT ===
            Subject: Data Structures & Algorithms
            Goal: Học Data Structures & Algorithms cho Full-stack Developer
            Goal Description: Học các cấu trúc dữ liệu và thuật toán cốt lõi, áp dụng vào phát triển frontend và backend để xây dựng ứng dụng web hoàn chỉnh
            Complexity Level: Advanced level, suitable for those who want to specialize and already have a foundational knowledge.

            === LANGUAGE REQUIREMENTS ===
            - Generate ALL content in Vietnamese language
            - CRITICAL: ALWAYS keep ALL technical terms, programming concepts, and technology names in ENGLISH
            - DO NOT translate technical terms to Vietnamese under any circumstances

            === STRUCTURE REQUIREMENTS ===
            - Exactly 6 chapters
            - Each chapter must have 4 to 5 lessons (minimum 4, maximum 5)
            - Approximately 30% of lessons should have quizzes (some lessons have quizzes, some don't)

            === CRITICAL INSTRUCTIONS ===
            1. Return ONLY valid, complete JSON (no markdown, no extra text, no explanations)
            2. Ensure ALL JSON brackets and braces are properly closed
            3. Do not truncate the response - provide the complete JSON structure

            IMPORTANT: Generate the complete JSON structure with all 6 chapters and their lessons. Do not truncate or abbreviate the response.
            """, "Complex Vietnamese DSA")
        };
    }

    private static (string prompt, string testName)[] CreateSimpleEnglishPrompt()
    {
        return new[]
        {
            ("""
            Generate a complete learning path structure in valid JSON format.

            === CONTEXT ===
            Subject: React
            Goal: Learn React Basics
            Complexity Level: Basic, suitable for beginners.

            === LANGUAGE REQUIREMENTS ===
            - Generate ALL content in English language

            === STRUCTURE REQUIREMENTS ===
            - Exactly 3 chapters
            - Each chapter must have 3 to 5 lessons
            - No quizzes needed

            === CRITICAL INSTRUCTIONS ===
            1. Return ONLY valid, complete JSON
            2. Ensure ALL JSON brackets and braces are properly closed

            IMPORTANT: Generate the complete JSON structure with all 3 chapters.
            """, "Simple English React")
        };
    }

    private static (string prompt, string testName)[] CreateVeryComplexPrompt()
    {
        return new[]
        {
            ("""
            Generate a complete learning path structure in valid JSON format for an extremely comprehensive course.

            === CONTEXT ===
            Subject: Full-Stack Web Development with Microservices Architecture
            Goal: Master Enterprise-Level Full-Stack Development
            Goal Description: Comprehensive training covering frontend frameworks, backend APIs, database design, DevOps practices, cloud deployment, monitoring, security, and microservices architecture for building scalable enterprise applications
            Complexity Level: Advanced level, suitable for those who want to specialize and already have a foundational knowledge.

            === LANGUAGE REQUIREMENTS ===
            - Generate ALL content in Vietnamese language
            - CRITICAL: ALWAYS keep ALL technical terms in ENGLISH

            === STRUCTURE REQUIREMENTS ===
            - Exactly 10 chapters
            - Each chapter must have 5 lessons
            - Approximately 40% of lessons should have quizzes

            === CRITICAL INSTRUCTIONS ===
            1. Return ONLY valid, complete JSON
            2. Generate the complete JSON structure with all 10 chapters and 50 lessons total

            IMPORTANT: This is a very large structure - ensure the response is complete and not truncated.
            """, "Very Complex Full-Stack")
        };
    }

    private static (string prompt, string testName)[] CreateEdgeCasePrompt()
    {
        return new[]
        {
            ("""
            Generate a learning path with special characters and edge cases.

            Subject: "Advanced C++ & System Programming"
            Goal: Learn C++/CLI, COM+, .NET Interop & Win32 API
            Description: Master advanced topics including: memory management, RAII patterns, template metaprogramming, STL containers, smart pointers, exception handling, multi-threading, async/await patterns, and system-level programming with special characters like: àáâãäåæçèéêëìíîïðñòóôõö÷øùúûüýþÿ

            Requirements:
            - 4 chapters
            - 3-4 lessons per chapter  
            - Include Vietnamese descriptions with technical terms in English
            - Handle special characters properly in JSON

            Return valid JSON only.
            """, "Edge Case Special Characters")
        };
    }

    private string TestJsonExtraction(string response)
    {
        // This would call the actual ExtractJsonFromResponse method
        // For now, simulate the logic
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

        if (response.StartsWith("{") && response.EndsWith("}"))
            return response;

        return null;
    }

    private bool TestJsonValidation(string jsonString)
    {
        try
        {
            using var document = JsonDocument.Parse(jsonString);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public class TestResult
    {
        public string TestName { get; set; } = "";
        public int Attempt { get; set; }
        public bool Success { get; set; }
        public TimeSpan Duration { get; set; }
        public int ChapterCount { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class LearningPathTestDto
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public List<ChapterTestDto>? Chapters { get; set; }
    }

    public class ChapterTestDto
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public int OrderIndex { get; set; }
        public List<LessonTestDto>? Lessons { get; set; }
    }

    public class LessonTestDto
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public List<object>? Quizzes { get; set; }
    }
}