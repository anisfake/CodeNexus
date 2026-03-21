using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.TutorChat.Commands.SendTutorMessage;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using Moq;
using Xunit;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.UnitTests.Features.TutorChat;

public class SendTutorMessageCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<IAIGeneratorService> _mockAiGenerator;
    private readonly Mock<IPlanUsageLimitService> _mockPlanUsageLimitService;
    private readonly SendTutorMessageCommandHandler _handler;

    public SendTutorMessageCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockAiGenerator = new Mock<IAIGeneratorService>();
        _mockPlanUsageLimitService = new Mock<IPlanUsageLimitService>();
        _mockPlanUsageLimitService.Setup(x => x.CheckTutorMessageAllowedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CodeNexus.Application.Common.Models.Result.Success());

        _handler = new SendTutorMessageCommandHandler(
            _mockContext.Object,
            _mockCurrentUserService.Object,
            _mockAiGenerator.Object,
            _mockPlanUsageLimitService.Object);
    }

    [Fact]
    public async Task Handle_WithValidInput_ReturnsTutorResponse()
    {
        var userId = Guid.NewGuid();
        var configId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var learningPathId = Guid.NewGuid();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);

        _mockContext.Setup(x => x.AIProviderConfigs).Returns(new[]
        {
            new AIProviderConfig
            {
                ConfigId = configId,
                UsageType = AIUsageType.Assistant,
                IsActive = true
            }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.LearningPaths).Returns(new[]
        {
            new LearningPath
            {
                PathId = learningPathId,
                UserId = userId,
                SubjectId = subjectId,
                Subject = new Subject { SubjectId = subjectId, Name = "C#" },
                Language = LanguageSelection.English,
                LearningPathGoals = new List<LearningPathGoal>()
            }
        }.BuildMockDbSet().Object);

        var conversations = new List<Conversation>
        {
            new()
            {
                ConversationId = Guid.NewGuid(),
                UserId = userId,
                ConfigId = configId,
                Title = "Tutor Chat"
            }
        };
        _mockContext.Setup(x => x.Conversations).Returns(conversations.BuildMockDbSet().Object);

        var messages = new List<Message>();
        var messagesDbSet = messages.BuildMockDbSet();
        messagesDbSet.Setup(x => x.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<Message>>(
                (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<Message>)null!));
        _mockContext.Setup(x => x.Messages).Returns(messagesDbSet.Object);

        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _mockAiGenerator.Setup(x => x.GenerateContentAsync(It.IsAny<string>(), AIUsageType.Assistant))
            .ReturnsAsync("This is a tutor response.");

        var command = new SendTutorMessageCommand(
            conversations[0].ConversationId,
            learningPathId,
            null,
            null,
            "Explain async/await");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(conversations[0].ConversationId, result.Value!.ConversationId);
        Assert.Equal("This is a tutor response.", result.Value.AssistantMessage);
    }

    [Fact]
    public async Task Handle_WhenTutorLimitExceeded_ReturnsFailure()
    {
        var userId = Guid.NewGuid();
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockPlanUsageLimitService.Setup(x => x.CheckTutorMessageAllowedAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CodeNexus.Application.Common.Models.Result.Failure("TUTOR_MESSAGE_LIMIT_EXCEEDED", "Limit reached"));

        var command = new SendTutorMessageCommand(null, Guid.NewGuid(), null, null, "Explain async/await");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("TUTOR_MESSAGE_LIMIT_EXCEEDED", result.ErrorCode);
    }
}
