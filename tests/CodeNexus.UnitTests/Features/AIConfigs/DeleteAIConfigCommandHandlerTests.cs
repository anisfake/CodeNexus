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
    public async Task Handle_WithValidInactiveConfig_ShouldDeleteSuccessfully()
    {
        var configId = Guid.NewGuid();
        var configs = new List<AIProviderConfig>
        {
            new() { ConfigId = configId, ProviderName = "OpenAI", IsActive = false },
            new() { ConfigId = Guid.NewGuid(), ProviderName = "Groq", IsActive = true }
        };

        SetupDbSet(configs);
        _mockContext.Setup(x => x.AIProviderConfigs.Remove(It.IsAny<AIProviderConfig>()));
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(new DeleteAIConfigCommand(configId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains("deleted successfully", result.Value);
        Assert.Contains("OpenAI", result.Value);
        _mockContext.Verify(x => x.AIProviderConfigs.Remove(It.IsAny<AIProviderConfig>()), Times.Once);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentConfig_ShouldReturnNotFound()
    {
        SetupDbSet(new List<AIProviderConfig>());

        var result = await _handler.Handle(new DeleteAIConfigCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("CONFIG_NOT_FOUND", result.ErrorCode);
        _mockContext.Verify(x => x.AIProviderConfigs.Remove(It.IsAny<AIProviderConfig>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenOnlyOneConfigExists_ShouldReturnFailure()
    {
        var configId = Guid.NewGuid();
        var configs = new List<AIProviderConfig>
        {
            new() { ConfigId = configId, ProviderName = "OpenAI", IsActive = false }
        };

        SetupDbSet(configs);

        var result = await _handler.Handle(new DeleteAIConfigCommand(configId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("CANNOT_DELETE_LAST_CONFIG", result.ErrorCode);
        _mockContext.Verify(x => x.AIProviderConfigs.Remove(It.IsAny<AIProviderConfig>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenConfigIsActive_ShouldReturnFailure()
    {
        var configId = Guid.NewGuid();
        var configs = new List<AIProviderConfig>
        {
            new() { ConfigId = configId, ProviderName = "OpenAI", IsActive = true },
            new() { ConfigId = Guid.NewGuid(), ProviderName = "Groq", IsActive = false }
        };

        SetupDbSet(configs);

        var result = await _handler.Handle(new DeleteAIConfigCommand(configId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("CANNOT_DELETE_ACTIVE_CONFIG", result.ErrorCode);
        _mockContext.Verify(x => x.AIProviderConfigs.Remove(It.IsAny<AIProviderConfig>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSaveChangesFails_ShouldReturnError()
    {
        var configId = Guid.NewGuid();
        var configs = new List<AIProviderConfig>
        {
            new() { ConfigId = configId, ProviderName = "Anthropic", IsActive = false },
            new() { ConfigId = Guid.NewGuid(), ProviderName = "Groq", IsActive = true }
        };

        SetupDbSet(configs);
        _mockContext.Setup(x => x.AIProviderConfigs.Remove(It.IsAny<AIProviderConfig>()));
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        var result = await _handler.Handle(new DeleteAIConfigCommand(configId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("ERROR", result.ErrorCode);
        Assert.Contains("Database error", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_ShouldClearCacheAfterDelete()
    {
        var configId = Guid.NewGuid();
        var configs = new List<AIProviderConfig>
        {
            new() { ConfigId = configId, ProviderName = "Google", IsActive = false },
            new() { ConfigId = Guid.NewGuid(), ProviderName = "Groq", IsActive = true }
        };

        SetupDbSet(configs);
        _mockContext.Setup(x => x.AIProviderConfigs.Remove(It.IsAny<AIProviderConfig>()));
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        object? cacheEntry;
        _mockCache.Setup(x => x.TryGetValue(It.IsAny<object>(), out cacheEntry)).Returns(true);
        _mockCache.Setup(x => x.Remove(It.IsAny<object>()));

        var result = await _handler.Handle(new DeleteAIConfigCommand(configId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _mockCache.Verify(x => x.Remove(It.IsAny<object>()), Times.Once);
    }

    private void SetupDbSet(List<AIProviderConfig> configs)
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
