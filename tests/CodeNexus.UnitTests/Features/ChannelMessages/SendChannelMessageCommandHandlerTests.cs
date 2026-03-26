using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.ChannelMessages.Commands.SendChannelMessage;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;

namespace CodeNexus.UnitTests.Features.ChannelMessages;

public class SendChannelMessageCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly SendChannelMessageCommandHandler _handler;

    public SendChannelMessageCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new SendChannelMessageCommandHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_ValidInput_ReturnsSuccess()
    {
        var userId = NewId.NextGuid();
        var conversationId = NewId.NextGuid();

        var users = new List<User>
        {
            new()
            {
                UserId = userId,
                Username = "mentor",
                FirstName = "An",
                LastName = "Nguyen"
            }
        };

        var conversations = new List<DirectConversation>
        {
            new()
            {
                ConversationId = conversationId,
                Category = SubjectCategory.Backend,
                ConversationType = ChatConversationType.Channel
            }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Users).Returns(users.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.DirectConversations).Returns(conversations.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.DirectMessages).Returns(new List<DirectMessage>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new SendChannelMessageCommand(SubjectCategory.Backend, " Xin chao channel ", DirectMessageType.Text);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Category.Should().Be(SubjectCategory.Backend);
        result.Value.Content.Should().Be("Xin chao channel");
        result.Value.MessageType.Should().Be(DirectMessageType.Text);
        result.Value.SenderName.Should().Be("An Nguyen");
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsFailure()
    {
        var userId = NewId.NextGuid();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Users).Returns(new List<User>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.DirectConversations).Returns(new List<DirectConversation>().BuildMockDbSet().Object);

        var command = new SendChannelMessageCommand(SubjectCategory.Cloud, "hello");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("USER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_ReplyToMessageInDifferentChannel_ReturnsMessageNotFound()
    {
        var userId = NewId.NextGuid();
        var backendConversationId = NewId.NextGuid();
        var cloudConversationId = NewId.NextGuid();
        var replyMessageId = NewId.NextGuid();

        var users = new List<User>
        {
            new() { UserId = userId, Username = "user" }
        };

        var conversations = new List<DirectConversation>
        {
            new() { ConversationId = backendConversationId, Category = SubjectCategory.Backend, ConversationType = ChatConversationType.Channel }
        };

        var messages = new List<DirectMessage>
        {
            new() { MessageId = replyMessageId, ConversationId = cloudConversationId, SenderId = userId, Content = "old" }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Users).Returns(users.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.DirectConversations).Returns(conversations.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.DirectMessages).Returns(messages.BuildMockDbSet().Object);

        var command = new SendChannelMessageCommand(SubjectCategory.Backend, "reply", DirectMessageType.Text, replyMessageId);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("MESSAGE_NOT_FOUND");
    }
}
