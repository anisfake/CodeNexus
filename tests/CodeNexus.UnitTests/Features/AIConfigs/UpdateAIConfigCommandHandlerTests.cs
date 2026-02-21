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
        var providerName = "OpenAI";
        var newApiKey = "new-api-key";
        var command = new UpdateAIConfigCommand(
            providerName,
            newApiKey,
            new Dictionary<string, object> { { "model", "gpt-4" } },
            true
        );

        var existingConfig = new AIProviderConfig
        {
            ProviderName = providerName,
            EncryptedApiKey = "old-encrypted-key",
            ConfigJson = "{\"model\":\"gpt-3.5\"}",
            IsEnabled = false,
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
        Assert.Equal(providerName, result.Value.ProviderName);
        Assert.True(result.Value.IsEnabled);
        _mockEncryptionService.Verify(x => x.Encrypt(newApiKey), Times.Once);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentProvider_ShouldReturnNotFound()
    {
        // Arrange
        var command = new UpdateAIConfigCommand("NonExistent", "api-key", null, null);
        SetupAIProviderConfigsDbSet(new List<AIProviderConfig>());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("PROVIDER_NOT_FOUND", result.ErrorCode);
        Assert.Contains("not found", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WithOnlyApiKeyUpdate_ShouldUpdateOnlyApiKey()
    {
        // Arrange
        var providerName = "Anthropic";
        var newApiKey = "new-api-key";
        var command = new UpdateAIConfigCommand(providerName, newApiKey, null, null);

        var existingConfig = new AIProviderConfig
        {
            ProviderName = providerName,
            EncryptedApiKey = "old-encrypted-key",
            ConfigJson = "{\"model\":\"claude-2\"}",
            IsEnabled = true,
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
        var providerName = "Google";
        var command = new UpdateAIConfigCommand(providerName, null, null, false);

        var existingConfig = new AIProviderConfig
        {
            ProviderName = providerName,
            EncryptedApiKey = "encrypted-key",
            ConfigJson = "{\"model\":\"gemini-pro\"}",
            IsEnabled = true,
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
        var command = new UpdateAIConfigCommand("OpenAI", "new-key", null, null);
        var existingConfig = new AIProviderConfig
        {
            ProviderName = "OpenAI",
            EncryptedApiKey = "old-key",
            ConfigJson = "{}",
            IsEnabled = true
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
