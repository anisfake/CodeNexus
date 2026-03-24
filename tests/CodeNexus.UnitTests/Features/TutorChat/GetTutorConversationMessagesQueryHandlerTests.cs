using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.TutorChat.Queries.GetTutorConversationMessages;
using CodeNexus.Domain.Entities;
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
            new Conversation { ConversationId = conversationId, UserId = userId, IsDeleted = false }
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
    }
}
