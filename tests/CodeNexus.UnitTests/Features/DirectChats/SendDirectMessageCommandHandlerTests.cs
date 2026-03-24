using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.DirectChats.Commands.SendDirectMessage;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;

namespace CodeNexus.UnitTests.Features.DirectChats;

public class SendDirectMessageCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly SendDirectMessageCommandHandler _handler;

    public SendDirectMessageCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new SendDirectMessageCommandHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_ValidInput_ReturnsSuccessAndUpdatesConversation()
    {
        var mentorId = NewId.NextGuid();
        var studentId = NewId.NextGuid();
        var conversationId = NewId.NextGuid();

        var conversation = new DirectConversation
        {
            ConversationId = conversationId,
            MentorId = mentorId,
            StudentId = studentId
        };

        var conversations = new List<DirectConversation> { conversation };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.DirectConversations).Returns(conversations.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.DirectMessages).Returns(new List<DirectMessage>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.DirectMessageReceipts).Returns(new List<DirectMessageReceipt>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new SendDirectMessageCommand(conversationId, "hello student", DirectMessageType.Text);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.ConversationId.Should().Be(conversationId);
        result.Value.SenderId.Should().Be(mentorId);
        conversation.LastMessagePreview.Should().Be("hello student");
        conversation.LastMessageAt.Should().NotBeNull();
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UserNotInConversation_ReturnsAccessDenied()
    {
        var mentorId = NewId.NextGuid();
        var studentId = NewId.NextGuid();
        var outsiderId = NewId.NextGuid();
        var conversationId = NewId.NextGuid();

        var conversations = new List<DirectConversation>
        {
            new()
            {
                ConversationId = conversationId,
                MentorId = mentorId,
                StudentId = studentId
            }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(outsiderId);
        _mockContext.Setup(x => x.DirectConversations).Returns(conversations.BuildMockDbSet().Object);

        var command = new SendDirectMessageCommand(conversationId, "hello", DirectMessageType.Text);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ACCESS_DENIED");
    }
}
