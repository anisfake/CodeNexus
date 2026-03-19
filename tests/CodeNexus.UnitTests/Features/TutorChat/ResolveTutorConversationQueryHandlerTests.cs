using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.TutorChat.Queries.ResolveTutorConversation;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.TutorChat;

public class ResolveTutorConversationQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly ResolveTutorConversationQueryHandler _handler;

    public ResolveTutorConversationQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new ResolveTutorConversationQueryHandler(
            _mockContext.Object,
            _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_WithMissingContext_ShouldReturnFailure()
    {
        var userId = Guid.NewGuid();
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);

        var result = await _handler.Handle(
            new ResolveTutorConversationQuery(null, null, null, true),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("CONTEXT_REQUIRED", result.ErrorCode);
    }

    [Fact]
    public async Task Handle_WithExistingConversation_ShouldReturnConversationId()
    {
        var userId = Guid.NewGuid();
        var lessonId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);

        _mockContext.Setup(x => x.Lessons).Returns(new[]
        {
            new Lesson
            {
                LessonId = lessonId,
                Chapter = new Chapter
                {
                    LearningPath = new LearningPath { UserId = userId }
                }
            }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.Conversations).Returns(new[]
        {
            new Conversation
            {
                ConversationId = conversationId,
                UserId = userId,
                LessonId = lessonId,
                IsDeleted = false
            }
        }.BuildMockDbSet().Object);

        var result = await _handler.Handle(
            new ResolveTutorConversationQuery(null, null, lessonId, true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(conversationId, result.Value.ConversationId);
        Assert.False(result.Value.Created);
    }

    [Fact]
    public async Task Handle_WhenMissingAndCreateFalse_ShouldReturnNotFound()
    {
        var userId = Guid.NewGuid();
        var learningPathId = Guid.NewGuid();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.LearningPaths).Returns(new[]
        {
            new LearningPath { PathId = learningPathId, UserId = userId }
        }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Conversations).Returns(new List<Conversation>().BuildMockDbSet().Object);

        var result = await _handler.Handle(
            new ResolveTutorConversationQuery(learningPathId, null, null, false),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("CONVERSATION_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task Handle_WhenMissingAndCreateTrue_ShouldCreateConversation()
    {
        var userId = Guid.NewGuid();
        var learningPathId = Guid.NewGuid();
        var configId = Guid.NewGuid();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);

        _mockContext.Setup(x => x.LearningPaths).Returns(new[]
        {
            new LearningPath { PathId = learningPathId, UserId = userId, Title = "Path" }
        }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Conversations).Returns(new List<Conversation>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.AIProviderConfigs).Returns(new[]
        {
            new AIProviderConfig { ConfigId = configId, UsageType = AIUsageType.Assistant, IsActive = true }
        }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(
            new ResolveTutorConversationQuery(learningPathId, null, null, true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Created);
        Assert.NotEqual(Guid.Empty, result.Value.ConversationId);

        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
