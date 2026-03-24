using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.AIConfigs.Queries.GetAllAIConfigs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.AIConfigs;

public class GetAllAIConfigsQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<IMemoryCache> _mockCache;
    private readonly GetAllAIConfigsQueryHandler _handler;

    public GetAllAIConfigsQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCache = new Mock<IMemoryCache>();
        _handler = new GetAllAIConfigsQueryHandler(_mockContext.Object, _mockCache.Object);
    }

    [Fact]
    public async Task Handle_WithValidConfigs_ShouldReturnAllConfigs()
    {
        // Arrange
        var configs = new List<AIProviderConfig>
        {
            new AIProviderConfig
            {
                ConfigId = Guid.NewGuid(),
                ProviderName = "Groq",
                EncryptedApiKey = "encrypted_key_1",
                ConfigJson = "{\"Model\":\"llama-3.3-70b-versatile\",\"MaxTokens\":8000}",
                UsageType = AIUsageType.StructureGeneration,
                AccessTier = AIAccessTier.Free,
                IsActive = true,
                LastUpdated = DateTime.UtcNow
            },
            new AIProviderConfig
            {
                ConfigId = Guid.NewGuid(),
                ProviderName = "OpenAI",
                EncryptedApiKey = "encrypted_key_2",
                ConfigJson = "{\"Model\":\"gpt-4\",\"MaxTokens\":4000}",
                UsageType = AIUsageType.ContentGeneration,
                AccessTier = AIAccessTier.Paid,
                IsActive = false,
                LastUpdated = DateTime.UtcNow.AddDays(-1)
            }
        };

        _mockContext.Setup(x => x.AIProviderConfigs).Returns(configs.BuildMockDbSet().Object);
        
        object? cacheEntry = null;
        _mockCache.Setup(x => x.TryGetValue(It.IsAny<object>(), out cacheEntry)).Returns(false);
        _mockCache.Setup(x => x.CreateEntry(It.IsAny<object>())).Returns(Mock.Of<ICacheEntry>());

        var query = new GetAllAIConfigsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal("Groq", result.Value[0].ProviderName);
        Assert.True(result.Value[0].IsActive);
        Assert.Equal(AIUsageType.StructureGeneration, result.Value[0].UsageType);
        Assert.Equal(AIAccessTier.Free, result.Value[0].AccessTier);
        Assert.Equal("OpenAI", result.Value[1].ProviderName);
        Assert.False(result.Value[1].IsActive);
        Assert.Equal(AIUsageType.ContentGeneration, result.Value[1].UsageType);
        Assert.Equal(AIAccessTier.Paid, result.Value[1].AccessTier);
    }

    [Fact]
    public async Task Handle_WithEmptyConfigs_ShouldReturnEmptyList()
    {
        // Arrange
        var configs = new List<AIProviderConfig>();

        _mockContext.Setup(x => x.AIProviderConfigs).Returns(configs.BuildMockDbSet().Object);
        
        object? cacheEntry = null;
        _mockCache.Setup(x => x.TryGetValue(It.IsAny<object>(), out cacheEntry)).Returns(false);
        _mockCache.Setup(x => x.CreateEntry(It.IsAny<object>())).Returns(Mock.Of<ICacheEntry>());

        var query = new GetAllAIConfigsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task Handle_WithInvalidConfigJson_ShouldReturnEmptyConfigData()
    {
        // Arrange
        var configs = new List<AIProviderConfig>
        {
            new AIProviderConfig
            {
                ConfigId = Guid.NewGuid(),
                ProviderName = "Groq",
                EncryptedApiKey = "encrypted_key",
                ConfigJson = "invalid json",
                UsageType = AIUsageType.Assistant,
                AccessTier = AIAccessTier.Free,
                IsActive = true,
                LastUpdated = DateTime.UtcNow
            }
        };

        _mockContext.Setup(x => x.AIProviderConfigs).Returns(configs.BuildMockDbSet().Object);
        
        object? cacheEntry = null;
        _mockCache.Setup(x => x.TryGetValue(It.IsAny<object>(), out cacheEntry)).Returns(false);
        _mockCache.Setup(x => x.CreateEntry(It.IsAny<object>())).Returns(Mock.Of<ICacheEntry>());

        var query = new GetAllAIConfigsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Single(result.Value);
        Assert.Empty(result.Value[0].ConfigJson);
    }
}
