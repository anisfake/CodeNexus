using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.TutorChat.Queries.GetTutorConversationSummaries;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.TutorChat;

public class GetTutorConversationSummariesQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;

    public GetTutorConversationSummariesQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
    }

    [Fact]
    public async Task Handle_WhenConversationNotFound_ReturnsFailure()
    {
        var userId = Guid.NewGuid();
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Conversations).Returns(new List<Conversation>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.ConversationSummaries).Returns(new List<ConversationSummary>().BuildMockDbSet().Object);

        var handler = new GetTutorConversationSummariesQueryHandler(_mockContext.Object, _mockCurrentUserService.Object);

        var result = await handler.Handle(new GetTutorConversationSummariesQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("CONVERSATION_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotOwnConversation_ReturnsAccessDenied()
    {
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(currentUserId);
        _mockContext.Setup(x => x.Conversations).Returns(new[]
        {
            new Conversation
            {
                ConversationId = conversationId,
                UserId = otherUserId,
                IsDeleted = false
            }
        }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.ConversationSummaries).Returns(new List<ConversationSummary>().BuildMockDbSet().Object);

        var handler = new GetTutorConversationSummariesQueryHandler(_mockContext.Object, _mockCurrentUserService.Object);

        var result = await handler.Handle(new GetTutorConversationSummariesQuery(conversationId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("ACCESS_DENIED", result.ErrorCode);
    }

    [Fact]
    public async Task Handle_WithValidConversation_ReturnsPagedSummaries()
    {
        var userId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Conversations).Returns(new[]
        {
            new Conversation
            {
                ConversationId = conversationId,
                UserId = userId,
                IsDeleted = false
            }
        }.BuildMockDbSet().Object);

        var now = DateTime.UtcNow;
        _mockContext.Setup(x => x.ConversationSummaries).Returns(new[]
        {
            new ConversationSummary
            {
                SummaryId = Guid.NewGuid(),
                ConversationId = conversationId,
                SummaryContent = "summary-1",
                MessageCount = 20,
                CreatedAt = now.AddMinutes(-5)
            },
            new ConversationSummary
            {
                SummaryId = Guid.NewGuid(),
                ConversationId = conversationId,
                SummaryContent = "summary-2",
                MessageCount = 15,
                CreatedAt = now.AddMinutes(-1)
            }
        }.BuildMockDbSet().Object);

        var handler = new GetTutorConversationSummariesQueryHandler(_mockContext.Object, _mockCurrentUserService.Object);

        var result = await handler.Handle(new GetTutorConversationSummariesQuery(conversationId, 1, 10), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value!.TotalCount);
        Assert.Equal(2, result.Value.Items!.Count());
    }
}
