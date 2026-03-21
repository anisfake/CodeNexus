using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.AIConfigs.Commands.CreateAIConfig;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace CodeNexus.UnitTests.Features.AIConfigs;

public class CreateAIConfigCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<IEncryptionService> _mockEncryptionService;
    private readonly Mock<IMemoryCache> _mockCache;
    private readonly CreateAIConfigCommandHandler _handler;

    public CreateAIConfigCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockEncryptionService = new Mock<IEncryptionService>();
        _mockCache = new Mock<IMemoryCache>();
        _handler = new CreateAIConfigCommandHandler(
            _mockContext.Object,
            _mockEncryptionService.Object,
            _mockCache.Object
        );
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldCreateConfigSuccessfully()
    {
        // Arrange
        var command = new CreateAIConfigCommand(
            "OpenAI",
            "test-api-key",
            new Dictionary<string, object> { { "model", "gpt-4" } },
            AIUsageType.StructureGeneration,
            AIAccessTier.Free,
            true
        );

        SetupAIProviderConfigsDbSet(new List<AIProviderConfig>());
        _mockEncryptionService.Setup(x => x.Encrypt(It.IsAny<string>())).Returns("encrypted-key");
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.ProviderName.Should().Be("OpenAI");
        result.Value.IsEnabled.Should().BeTrue();
        _mockEncryptionService.Verify(x => x.Encrypt("test-api-key"), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateApiKey_ShouldReturnFailure()
    {
        // Arrange
        var existingConfig = new AIProviderConfig
        {
            ConfigId = Guid.NewGuid(),
            ProviderName = "OpenAI",
            EncryptedApiKey = "encrypted-existing-key",
            ConfigJson = "{}",
            IsActive = true
        };

        var command = new CreateAIConfigCommand(
            "Groq",  // Different provider but same API key
            "existing-api-key",
            new Dictionary<string, object>(),
            AIUsageType.ContentGeneration,
            AIAccessTier.Free,
            true
        );

        SetupAIProviderConfigsDbSet(new List<AIProviderConfig> { existingConfig });
        _mockEncryptionService.Setup(x => x.Decrypt("encrypted-existing-key")).Returns("existing-api-key");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("DUPLICATE_KEY");
    }

    [Fact]
    public async Task Handle_WithComplexConfigJson_ShouldCreateSuccessfully()
    {
        // Arrange
        var configJson = new Dictionary<string, object>
        {
            { "model", "gpt-4" },
            { "temperature", 0.7 },
            { "max_tokens", 1000 }
        };

        var command = new CreateAIConfigCommand(
            "Groq",
            "groq-api-key",
            configJson,
            AIUsageType.Verification,
            AIAccessTier.Paid,
            false
        );

        SetupAIProviderConfigsDbSet(new List<AIProviderConfig>());
        _mockEncryptionService.Setup(x => x.Encrypt(It.IsAny<string>())).Returns("encrypted-groq-key");
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.ProviderName.Should().Be("Groq");
        result.Value.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_SaveChangesFails_ShouldReturnFailure()
    {
        // Arrange
        var command = new CreateAIConfigCommand(
            "TestProvider",
            "test-key",
            new Dictionary<string, object>(),
            AIUsageType.Assistant,
            AIAccessTier.Free,
            true
        );

        SetupAIProviderConfigsDbSet(new List<AIProviderConfig>());
        _mockEncryptionService.Setup(x => x.Encrypt(It.IsAny<string>())).Returns("encrypted-key");
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ERROR");
        result.ErrorMessage.Should().Contain("Database error");
    }

    [Fact]
    public async Task Handle_ShouldClearCache_AfterCreatingConfig()
    {
        // Arrange
        var command = new CreateAIConfigCommand(
            "NewProvider",
            "api-key",
            new Dictionary<string, object>(),
            AIUsageType.StructureGeneration,
            AIAccessTier.Free,
            true
        );

        SetupAIProviderConfigsDbSet(new List<AIProviderConfig>());
        _mockEncryptionService.Setup(x => x.Encrypt(It.IsAny<string>())).Returns("encrypted-key");
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockCache.Verify(x => x.Remove("ai_configs_all"), Times.Once);
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
