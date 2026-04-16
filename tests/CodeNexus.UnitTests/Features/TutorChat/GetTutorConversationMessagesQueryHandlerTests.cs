using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.TutorChat.Queries.GetTutorConversationMessages;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.TutorChat;

public class GetTutorConversationMessagesQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly GetTutorConversationMessagesQueryHandler _handler;

    public GetTutorConversationMessagesQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockContext.Setup(x => x.AIUsageLogs).Returns(new List<AIUsageLog>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.AIProviderConfigs).Returns(new List<AIProviderConfig>().BuildMockDbSet().Object);
        _handler = new GetTutorConversationMessagesQueryHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_WithMessages_ReturnsPagedResults()
    {
        var userId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);

        _mockContext.Setup(x => x.Conversations).Returns(new[]
        {
            new Conversation { ConversationId = conversationId, UserId = userId, IsDeleted = false, ConfigId = Guid.NewGuid() }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.Messages).Returns(new[]
        {
            new Message { MessageId = Guid.NewGuid(), ConversationId = conversationId, Content = "USER: Hi", CreatedAt = DateTime.UtcNow.AddMinutes(-2) },
            new Message { MessageId = Guid.NewGuid(), ConversationId = conversationId, Content = "ASSISTANT: Hello", CreatedAt = DateTime.UtcNow.AddMinutes(-1) }
        }.BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetTutorConversationMessagesQuery(conversationId, 1, 30), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.Items[0].Role.Should().Be("user");
        result.Value.Items[1].Role.Should().Be("assistant");
        result.Value.ContextUsagePercent.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithAssistantUsageLog_ShouldReturnContextUsagePercent()
    {
        var userId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var configId = Guid.NewGuid();
        var assistantTime = DateTime.UtcNow.AddSeconds(-5);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);

        _mockContext.Setup(x => x.Conversations).Returns(new[]
        {
            new Conversation { ConversationId = conversationId, UserId = userId, IsDeleted = false, ConfigId = configId }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.Messages).Returns(new[]
        {
            new Message { MessageId = Guid.NewGuid(), ConversationId = conversationId, Content = "USER: Compare clean architecture and mvc", CreatedAt = assistantTime.AddSeconds(-2) },
            new Message { MessageId = Guid.NewGuid(), ConversationId = conversationId, Content = "ASSISTANT: ...", CreatedAt = assistantTime }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.AIProviderConfigs).Returns(new[]
        {
            new AIProviderConfig
            {
                ConfigId = configId,
                UsageType = AIUsageType.Assistant,
                AccessTier = AIAccessTier.Paid,
                IsActive = true,
                ConfigJson = "{\"model\":\"mistral-small-latest\",\"contextWindow\":200000}"
            }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.AIUsageLogs).Returns(new[]
        {
            new AIUsageLog
            {
                UsageLogId = Guid.NewGuid(),
                UserId = userId,
                UsageType = AIUsageType.Assistant,
                Model = "mistral-small-latest",
                InputTokens = 1300,
                OutputTokens = 600,
                TotalTokens = 1900,
                CreatedAt = assistantTime.AddSeconds(-1)
            }
        }.BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetTutorConversationMessagesQuery(conversationId, 1, 30), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ContextUsagePercent.Should().Be(0.65d);
    }

    [Fact]
    public async Task Handle_WithRuntimeContextBudgetInChatPolicy_ShouldUseRuntimeBudgetForContextUsage()
    {
        var userId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var configId = Guid.NewGuid();
        var assistantTime = DateTime.UtcNow.AddSeconds(-5);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);

        _mockContext.Setup(x => x.Conversations).Returns(new[]
        {
            new Conversation { ConversationId = conversationId, UserId = userId, IsDeleted = false, ConfigId = configId }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.Messages).Returns(new[]
        {
            new Message { MessageId = Guid.NewGuid(), ConversationId = conversationId, Content = "USER: Explain DI", CreatedAt = assistantTime.AddSeconds(-2) },
            new Message { MessageId = Guid.NewGuid(), ConversationId = conversationId, Content = "ASSISTANT: ...", CreatedAt = assistantTime }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.AIProviderConfigs).Returns(new[]
        {
            new AIProviderConfig
            {
                ConfigId = configId,
                UsageType = AIUsageType.Assistant,
                AccessTier = AIAccessTier.Paid,
                IsActive = true,
                ConfigJson = "{\"model\":\"mistral-small-latest\",\"contextWindow\":200000,\"chatPolicy\":{\"runtimeContextBudget\":24000}}"
            }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.AIUsageLogs).Returns(new[]
        {
            new AIUsageLog
            {
                UsageLogId = Guid.NewGuid(),
                UserId = userId,
                UsageType = AIUsageType.Assistant,
                Model = "mistral-small-latest",
                InputTokens = 1200,
                OutputTokens = 300,
                TotalTokens = 1500,
                CreatedAt = assistantTime.AddSeconds(-1)
            }
        }.BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetTutorConversationMessagesQuery(conversationId, 1, 30), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ContextUsagePercent.Should().Be(5.0d);
    }
}
