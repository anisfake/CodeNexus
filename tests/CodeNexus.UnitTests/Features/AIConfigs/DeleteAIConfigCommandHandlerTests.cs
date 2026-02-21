using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.AIConfigs.Commands.DeleteAIConfig;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Xunit;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.UnitTests.Features.AIConfigs;

public class DeleteAIConfigCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<IMemoryCache> _mockCache;
    private readonly DeleteAIConfigCommandHandler _handler;

    public DeleteAIConfigCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCache = new Mock<IMemoryCache>();
        _handler = new DeleteAIConfigCommandHandler(_mockContext.Object, _mockCache.Object);
    }

    [Fact]
    public async Task Handle_WithValidProviderName_ShouldDeleteConfigSuccessfully()
    {
        // Arrange
        var providerName = "OpenAI";
        var command = new DeleteAIConfigCommand(providerName);

        var config = new AIProviderConfig
        {
            ProviderName = providerName,
            EncryptedApiKey = "encrypted-key",
            ConfigJson = "{}",
            IsEnabled = true,
            LastUpdated = DateTime.Now
        };

        SetupAIProviderConfigsDbSet(new List<AIProviderConfig> { config });
        _mockContext.Setup(x => x.AIProviderConfigs.Remove(It.IsAny<AIProviderConfig>()));
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains("deleted successfully", result.Value);
        Assert.Contains(providerName, result.Value);
        _mockContext.Verify(x => x.AIProviderConfigs.Remove(It.IsAny<AIProviderConfig>()), Times.Once);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentProvider_ShouldReturnNotFound()
    {
        // Arrange
        var command = new DeleteAIConfigCommand("NonExistent");
        SetupAIProviderConfigsDbSet(new List<AIProviderConfig>());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("PROVIDER_NOT_FOUND", result.ErrorCode);
        Assert.Contains("not found", result.ErrorMessage);
        _mockContext.Verify(x => x.AIProviderConfigs.Remove(It.IsAny<AIProviderConfig>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSaveChangesFails_ShouldReturnError()
    {
        // Arrange
        var providerName = "Anthropic";
        var command = new DeleteAIConfigCommand(providerName);

        var config = new AIProviderConfig
        {
            ProviderName = providerName,
            EncryptedApiKey = "encrypted-key",
            ConfigJson = "{}",
            IsEnabled = true
        };

        SetupAIProviderConfigsDbSet(new List<AIProviderConfig> { config });
        _mockContext.Setup(x => x.AIProviderConfigs.Remove(It.IsAny<AIProviderConfig>()));
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("ERROR", result.ErrorCode);
        Assert.Contains("Database error", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_ShouldClearCache()
    {
        // Arrange
        var providerName = "Google";
        var command = new DeleteAIConfigCommand(providerName);

        var config = new AIProviderConfig
        {
            ProviderName = providerName,
            EncryptedApiKey = "encrypted-key",
            ConfigJson = "{}",
            IsEnabled = true
        };

        SetupAIProviderConfigsDbSet(new List<AIProviderConfig> { config });
        _mockContext.Setup(x => x.AIProviderConfigs.Remove(It.IsAny<AIProviderConfig>()));
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        object? cacheEntry;
        _mockCache.Setup(x => x.TryGetValue(It.IsAny<object>(), out cacheEntry)).Returns(true);
        _mockCache.Setup(x => x.Remove(It.IsAny<object>()));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        _mockCache.Verify(x => x.Remove(It.IsAny<object>()), Times.Once);
    }

    private void SetupAIProviderConfigsDbSet(List<AIProviderConfig> configs)
    {
        var queryable = new TestAsyncEnumerable<AIProviderConfig>(configs);
        var dbSetMock = new Mock<DbSet<AIProviderConfig>>();
        dbSetMock.As<IQueryable<AIProviderConfig>>().Setup(m => m.Provider).Returns(queryable.AsQueryable().Provider);
        dbSetMock.As<IQueryable<AIProviderConfig>>().Setup(m => m.Expression).Returns(queryable.AsQueryable().Expression);
        dbSetMock.As<IQueryable<AIProviderConfig>>().Setup(m => m.ElementType).Returns(queryable.AsQueryable().ElementType);
        dbSetMock.As<IQueryable<AIProviderConfig>>().Setup(m => m.GetEnumerator()).Returns(queryable.AsQueryable().GetEnumerator());
        dbSetMock.As<IAsyncEnumerable<AIProviderConfig>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(queryable.GetAsyncEnumerator());
        _mockContext.Setup(x => x.AIProviderConfigs).Returns(dbSetMock.Object);
    }
}
