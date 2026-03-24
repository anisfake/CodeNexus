using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.AIConfigs.Commands.UpdateAIConfig;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Xunit;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.UnitTests.Features.AIConfigs;

public class UpdateAIConfigCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<IEncryptionService> _mockEncryptionService;
    private readonly Mock<IMemoryCache> _mockCache;
    private readonly UpdateAIConfigCommandHandler _handler;

    public UpdateAIConfigCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockEncryptionService = new Mock<IEncryptionService>();
        _mockCache = new Mock<IMemoryCache>();
        _handler = new UpdateAIConfigCommandHandler(
            _mockContext.Object,
            _mockEncryptionService.Object,
            _mockCache.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldUpdateConfigSuccessfully()
    {
        // Arrange
        var configId = Guid.NewGuid();
        var newApiKey = "new-api-key";
        var command = new UpdateAIConfigCommand(
            configId,
            null,  // ProviderName
            newApiKey,
            new Dictionary<string, object> { { "model", "gpt-4" } },
            true,  // IsActive
            null,  // UsageType
            null   // AccessTier
        );

        var existingConfig = new AIProviderConfig
        {
            ConfigId = configId,
            ProviderName = "OpenAI",
            EncryptedApiKey = "old-encrypted-key",
            ConfigJson = "{\"model\":\"gpt-3.5\"}",
            IsActive = false,
            LastUpdated = DateTime.Now.AddDays(-1)
        };

        SetupAIProviderConfigsDbSet(new List<AIProviderConfig> { existingConfig });
        _mockEncryptionService.Setup(x => x.Encrypt(newApiKey)).Returns("new-encrypted-key");
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("OpenAI", result.Value.ProviderName);
        Assert.True(result.Value.IsEnabled);
        _mockEncryptionService.Verify(x => x.Encrypt(newApiKey), Times.Once);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentConfig_ShouldReturnNotFound()
    {
        // Arrange
        var command = new UpdateAIConfigCommand(Guid.NewGuid(), null, "api-key", null, null, null, null);
        SetupAIProviderConfigsDbSet(new List<AIProviderConfig>());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("CONFIG_NOT_FOUND", result.ErrorCode);
        Assert.Contains("not found", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WithOnlyApiKeyUpdate_ShouldUpdateOnlyApiKey()
    {
        // Arrange
        var configId = Guid.NewGuid();
        var newApiKey = "new-api-key";
        var command = new UpdateAIConfigCommand(configId, null, newApiKey, null, null, null, null);

        var existingConfig = new AIProviderConfig
        {
            ConfigId = configId,
            ProviderName = "Anthropic",
            EncryptedApiKey = "old-encrypted-key",
            ConfigJson = "{\"model\":\"claude-2\"}",
            IsActive = true,
            LastUpdated = DateTime.Now.AddDays(-1)
        };

        SetupAIProviderConfigsDbSet(new List<AIProviderConfig> { existingConfig });
        _mockEncryptionService.Setup(x => x.Encrypt(newApiKey)).Returns("new-encrypted-key");
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        _mockEncryptionService.Verify(x => x.Encrypt(newApiKey), Times.Once);
    }

    [Fact]
    public async Task Handle_WithOnlyIsEnabledUpdate_ShouldUpdateOnlyIsEnabled()
    {
        // Arrange
        var configId = Guid.NewGuid();
        var command = new UpdateAIConfigCommand(configId, null, null, null, false, null, null);

        var existingConfig = new AIProviderConfig
        {
            ConfigId = configId,
            ProviderName = "Google",
            EncryptedApiKey = "encrypted-key",
            ConfigJson = "{\"model\":\"gemini-pro\"}",
            IsActive = true,
            LastUpdated = DateTime.Now.AddDays(-1)
        };

        SetupAIProviderConfigsDbSet(new List<AIProviderConfig> { existingConfig });
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsEnabled);
        _mockEncryptionService.Verify(x => x.Encrypt(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSaveChangesFails_ShouldReturnError()
    {
        // Arrange
        var configId = Guid.NewGuid();
        var command = new UpdateAIConfigCommand(configId, null, "new-key", null, null, null, null);
        var existingConfig = new AIProviderConfig
        {
            ConfigId = configId,
            ProviderName = "OpenAI",
            EncryptedApiKey = "old-key",
            ConfigJson = "{}",
            IsActive = true
        };

        SetupAIProviderConfigsDbSet(new List<AIProviderConfig> { existingConfig });
        _mockEncryptionService.Setup(x => x.Encrypt(It.IsAny<string>())).Returns("encrypted");
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("ERROR", result.ErrorCode);
        Assert.Contains("Database error", result.ErrorMessage);
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
