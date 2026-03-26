using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.ChannelMessages.Queries.GetChannelMessages;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;

namespace CodeNexus.UnitTests.Features.ChannelMessages;

public class GetChannelMessagesQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly GetChannelMessagesQueryHandler _handler;

    public GetChannelMessagesQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new GetChannelMessagesQueryHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_ValidInput_ReturnsPagedMessages()
    {
        var userId = NewId.NextGuid();
        var subjectId = NewId.NextGuid();
        var conversationId = NewId.NextGuid();

        var sender1 = new User { UserId = userId, Username = "mentor", FirstName = "An", LastName = "Nguyen" };
        var sender2 = new User { UserId = NewId.NextGuid(), Username = "student01" };

        var subjects = new List<Subject>
        {
            new()
            {
                SubjectId = subjectId,
                CreatedByUserId = userId,
                Name = "Backend",
                Category = SubjectCategory.Backend,
                CreatedByUser = sender1
            }
        };

        var conversations = new List<DirectConversation>
        {
            new()
            {
                ConversationId = conversationId,
                SubjectId = subjectId,
                Category = SubjectCategory.Backend,
                ConversationType = ChatConversationType.Channel
            }
        };

        var messages = new List<DirectMessage>
        {
            new()
            {
                MessageId = NewId.NextGuid(),
                ConversationId = conversationId,
                SenderId = sender1.UserId,
                Sender = sender1,
                Content = "msg 1",
                MessageType = DirectMessageType.Text,
                SentAt = DateTime.UtcNow.AddMinutes(-2)
            },
            new()
            {
                MessageId = NewId.NextGuid(),
                ConversationId = conversationId,
                SenderId = sender2.UserId,
                Sender = sender2,
                Content = "msg 2",
                MessageType = DirectMessageType.Emoji,
                SentAt = DateTime.UtcNow.AddMinutes(-1)
            }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Subjects).Returns(subjects.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.DirectConversations).Returns(conversations.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.DirectMessages).Returns(messages.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.DirectMessageReceipts).Returns(new List<DirectMessageReceipt>().BuildMockDbSet().Object);

        var query = new GetChannelMessagesQuery(subjectId, SubjectCategory.Backend, 1, 30);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.TotalCount.Should().Be(2);
        result.Value.Items[0].Content.Should().Be("msg 1");
        result.Value.Items[0].SenderName.Should().Be("An Nguyen");
        result.Value.Items[1].SenderName.Should().Be("student01");
    }

    [Fact]
    public async Task Handle_SubjectNotFound_ReturnsFailure()
    {
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(NewId.NextGuid());
        _mockContext.Setup(x => x.Subjects).Returns(new List<Subject>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath>().BuildMockDbSet().Object);

        var query = new GetChannelMessagesQuery(NewId.NextGuid(), SubjectCategory.Cloud, 1, 10);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("SUBJECT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_UserHasNoAccess_ReturnsAccessDenied()
    {
        var userId = NewId.NextGuid();
        var ownerId = NewId.NextGuid();
        var subjectId = NewId.NextGuid();

        var subjects = new List<Subject>
        {
            new()
            {
                SubjectId = subjectId,
                CreatedByUserId = ownerId,
                Name = "Cloud",
                Category = SubjectCategory.Cloud,
                CreatedByUser = new User { UserId = ownerId, Username = "owner" }
            }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Subjects).Returns(subjects.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath>().BuildMockDbSet().Object);

        var query = new GetChannelMessagesQuery(subjectId, SubjectCategory.Cloud, 1, 10);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ACCESS_DENIED");
    }
}
