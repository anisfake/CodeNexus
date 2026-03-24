using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.Infrastructure.Services;
using CodeNexus.UnitTests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace CodeNexus.UnitTests.Services;

public class GroqServiceWithCacheTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<IAIConfigCacheService> _mockCacheService;
    private readonly Mock<IEncryptionService> _mockEncryptionService;
    private readonly HttpClient _httpClient;
    private readonly GroqServiceWithCache _service;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<ISubscriptionAccessService> _mockSubscriptionAccessService;

    public GroqServiceWithCacheTests()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCacheService = new Mock<IAIConfigCacheService>();
        _mockEncryptionService = new Mock<IEncryptionService>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockSubscriptionAccessService = new Mock<ISubscriptionAccessService>();
        _mockSubscriptionAccessService.Setup(x => x.CanUsePaidModelsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _service = new GroqServiceWithCache(_httpClient, _mockContext.Object, _mockCacheService.Object, _mockEncryptionService.Object, _mockCurrentUserService.Object, _mockSubscriptionAccessService.Object);

        SetupAIProviderConfigsDbSet();
        SetupUsersDbSet();
    }

    [Fact]
    public async Task GenerateStructureAsync_WithValidJson_ShouldSucceed()
    {
        // Arrange
        var validJsonResponse = """
        {
            "title": "Test Learning Path",
            "description": "Test description",
            "chapters": [
                {
                    "title": "Chapter 1",
                    "description": "Chapter description",
                    "orderIndex": 0,
                    "lessons": [
                        {
                            "title": "Lesson 1",
                            "description": "Lesson description",
                            "quizzes": []
                        }
                    ]
                }
            ]
        }
        """;

        SetupHttpResponse(CreateGroqResponse(validJsonResponse));
        SetupCacheService();

        // Act
        var result = await _service.GenerateStructureAsync<TestDto>("test prompt");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test Learning Path", result.Title);
        Assert.Single(result.Chapters);
    }

    [Fact]
    public async Task GenerateStructureAsync_WithTruncatedJson_ShouldRetryAndFail()
    {
        // Arrange - Simulate truncated JSON (missing closing braces)
        var truncatedJsonResponse = """
        {
            "title": "Test Learning Path",
            "description": "Test description",
            "chapters": [
                {
                    "title": "Chapter 1",
                    "description": "Chapter description",
                    "orderIndex": 0,
                    "lessons": [
                        {
                            "title": "Lesson 1",
                            "description": "Lesson description"
        """;

        SetupHttpResponse(CreateGroqResponse(truncatedJsonResponse, "length")); // finish_reason: length indicates truncation
        SetupCacheService();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.GenerateStructureAsync<TestDto>("test prompt"));
        
        Assert.Contains("Failed to generate structure after 3 attempts", exception.Message);
    }

    [Fact]
    public async Task GenerateStructureAsync_WithMarkdownWrappedJson_ShouldExtractCorrectly()
    {
        // Arrange
        var markdownWrappedResponse = """
        Here's the JSON structure you requested:

        ```json
        {
            "title": "Test Learning Path",
            "description": "Test description",
            "chapters": []
        }
        ```

        This should work perfectly for your needs.
        """;

        SetupHttpResponse(CreateGroqResponse(markdownWrappedResponse));
        SetupCacheService();

        // Act
        var result = await _service.GenerateStructureAsync<TestDto>("test prompt");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test Learning Path", result.Title);
    }

    [Fact]
    public async Task GenerateStructureAsync_WithInvalidJson_ShouldRetryAndFail()
    {
        // Arrange - Invalid JSON with syntax errors
        var invalidJsonResponse = """
        {
            "title": "Test Learning Path",
            "description": "Test description"
            "chapters": [  // Missing comma here
                {
                    "title": "Chapter 1"
                    "description": "Chapter description"  // Missing comma here
                }
            ]
        }
        """;

        SetupHttpResponse(CreateGroqResponse(invalidJsonResponse));
        SetupCacheService();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.GenerateStructureAsync<TestDto>("test prompt"));
        
        Assert.Contains("Failed to generate structure after 3 attempts", exception.Message);
    }

    [Theory]
    [InlineData("```json\n{\"title\":\"Test\"}\n```")]
    [InlineData("```\n{\"title\":\"Test\"}\n```")]
    [InlineData("JSON: {\"title\":\"Test\"}")]
    [InlineData("Here's the JSON:\n{\"title\":\"Test\"}")]
    [InlineData("{\"title\":\"Test\"}")]
    public async Task GenerateStructureAsync_WithVariousJsonFormats_ShouldExtractCorrectly(string responseFormat)
    {
        // Arrange
        SetupHttpResponse(CreateGroqResponse(responseFormat));
        SetupCacheService();

        // Act
        var result = await _service.GenerateStructureAsync<SimpleTestDto>("test prompt");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test", result.Title);
    }

    private void SetupHttpResponse(string responseContent)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
    }

    private void SetupCacheService()
    {
        _mockCacheService
            .Setup(x => x.GetApiKeyAsync(It.IsAny<AIUsageType>(), It.IsAny<AIAccessTier>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
    }

    private void SetupAIProviderConfigsDbSet()
    {
        _mockEncryptionService.Setup(x => x.Decrypt("encrypted-api-key")).Returns("test-api-key");

        _mockContext
            .Setup(x => x.AIProviderConfigs)
            .Returns(new List<AIProviderConfig>
            {
                new()
                {
                    ConfigId = Guid.NewGuid(),
                    ProviderName = "Groq",
                    UsageType = AIUsageType.StructureGeneration,
                    AccessTier = AIAccessTier.Free,
                    EncryptedApiKey = "encrypted-api-key",
                    ConfigJson = "{\"Model\":\"openai/gpt-oss-120b\",\"MaxTokens\":8192,\"Temperature\":0.3,\"RequestTimeoutSeconds\":30}",
                    IsActive = true
                }
            }.BuildMockDbSet().Object);
    }

    private void SetupUsersDbSet()
    {
        var userId = Guid.NewGuid();
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);

        _mockContext
            .Setup(x => x.Users)
            .Returns(new List<User>
            {
                new()
                {
                    UserId = userId,
                    PlanExpiresAt = null
                }
            }.BuildMockDbSet().Object);
    }

    private static string CreateGroqResponse(string content, string finishReason = "stop")
    {
        var groqResponse = new
        {
            choices = new[]
            {
                new
                {
                    message = new { content },
                    finish_reason = finishReason
                }
            }
        };

        return JsonSerializer.Serialize(groqResponse);
    }

    public class TestDto
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public List<ChapterDto> Chapters { get; set; } = new();
    }

    public class ChapterDto
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public int OrderIndex { get; set; }
        public List<LessonDto> Lessons { get; set; } = new();
    }

    public class LessonDto
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public List<object> Quizzes { get; set; } = new();
    }

    public class SimpleTestDto
    {
        public string Title { get; set; } = "";
    }
}
