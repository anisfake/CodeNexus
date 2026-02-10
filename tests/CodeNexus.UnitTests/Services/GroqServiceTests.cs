using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Infrastructure.Services;
using CodeNexus.Infrastructure.Settings;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using Xunit;

namespace CodeNexus.UnitTests.Services;

public class GroqServiceTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly GroqSettings _groqSettings;
    private readonly GroqService _groqService;

    public GroqServiceTests()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        
        _groqSettings = new GroqSettings
        {
            ApiKey = "test-api-key",
            Model = "test-model",
            MaxTokens = 1000,
            Temperature = 0.7f,
            MaxRetries = 3,
            RetryDelayMilliseconds = 100,
            RequestTimeoutSeconds = 30
        };

        var options = Options.Create(_groqSettings);
        _groqService = new GroqService(options, _httpClient);
    }

    [Fact]
    public async Task GenerateStructureAsync_WithValidResponse_ShouldDeserializeCorrectly()
    {
        // Arrange
        var prompt = "Generate a learning path";
        var jsonResponse = @"{
            ""choices"": [{
                ""message"": {
                    ""content"": ""{\""title\"": \""Test Path\"", \""description\"": \""Test\"", \""chapters\"": []}""
                }
            }]
        }";

        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse)
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(responseMessage);

        // Act
        var result = await _groqService.GenerateStructureAsync<LearningPathSkeletonDto>(prompt);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test Path", result.Title);
        Assert.Equal("Test", result.Description);
    }

    [Fact]
    public async Task GenerateStructureAsync_WithJsonInMarkdownCodeBlock_ShouldExtractCorrectly()
    {
        // Arrange
        var prompt = "Generate a learning path";
        var jsonResponse = @"{
            ""choices"": [{
                ""message"": {
                    ""content"": ""```json\n{\""title\"": \""Test Path\"", \""description\"": \""Test\"", \""chapters\"": []}\n```""
                }
            }]
        }";

        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse)
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(responseMessage);

        // Act
        var result = await _groqService.GenerateStructureAsync<LearningPathSkeletonDto>(prompt);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test Path", result.Title);
    }

    [Fact]
    public async Task GenerateStructureAsync_WithEmptyPrompt_ShouldThrowException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _groqService.GenerateStructureAsync<LearningPathSkeletonDto>(string.Empty)
        );
    }

    [Fact]
    public async Task GenerateStructureAsync_WithApiError_ShouldThrowException()
    {
        // Arrange
        var prompt = "Generate a learning path";
        var responseMessage = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("API Error")
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(responseMessage);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _groqService.GenerateStructureAsync<LearningPathSkeletonDto>(prompt)
        );
    }

    [Fact]
    public async Task GenerateStructureAsync_WithInvalidJson_ShouldThrowException()
    {
        // Arrange
        var prompt = "Generate a learning path";
        var jsonResponse = @"{
            ""choices"": [{
                ""message"": {
                    ""content"": ""invalid json""
                }
            }]
        }";

        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse)
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(responseMessage);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _groqService.GenerateStructureAsync<LearningPathSkeletonDto>(prompt)
        );
    }

    [Fact]
    public async Task GenerateContentAsync_WithValidResponse_ShouldReturnContent()
    {
        // Arrange
        var prompt = "Generate content";
        var content = "This is generated content";
        var jsonResponse = @"{
            ""choices"": [{
                ""message"": {
                    ""content"": """ + content + @"""
                }
            }]
        }";

        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse)
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(responseMessage);

        // Act
        var result = await _groqService.GenerateContentAsync(prompt);

        // Assert
        Assert.Equal(content, result);
    }

    [Fact]
    public async Task GenerateContentAsync_WithEmptyPrompt_ShouldThrowException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _groqService.GenerateContentAsync(string.Empty)
        );
    }
}
