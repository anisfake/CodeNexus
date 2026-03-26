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
    public async Task Handle_UserIsSubjectOwner_ReturnsSuccess()
    {
        var userId = NewId.NextGuid();
        var subjectId = NewId.NextGuid();
        var conversationId = NewId.NextGuid();

        var subjects = new List<Subject>
        {
            new()
            {
                SubjectId = subjectId,
                CreatedByUserId = userId,
                Name = "Backend",
                Category = SubjectCategory.Backend,
                CreatedByUser = new User { UserId = userId, Username = "mentor" }
            }
        };

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
                SubjectId = subjectId,
                Category = SubjectCategory.Backend,
                ConversationType = ChatConversationType.Channel
            }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Subjects).Returns(subjects.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Users).Returns(users.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.DirectConversations).Returns(conversations.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.DirectMessages).Returns(new List<DirectMessage>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new SendChannelMessageCommand(subjectId, SubjectCategory.Backend, " Xin chao channel ", DirectMessageType.Text);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.SubjectId.Should().Be(subjectId);
        result.Value.Category.Should().Be(SubjectCategory.Backend);
        result.Value.Content.Should().Be("Xin chao channel");
        result.Value.MessageType.Should().Be(DirectMessageType.Text);
        result.Value.SenderName.Should().Be("An Nguyen");
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SubjectNotFound_ReturnsFailure()
    {
        var userId = NewId.NextGuid();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Subjects).Returns(new List<Subject>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath>().BuildMockDbSet().Object);

        var command = new SendChannelMessageCommand(NewId.NextGuid(), SubjectCategory.Cloud, "hello");

        var result = await _handler.Handle(command, CancellationToken.None);

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

        var command = new SendChannelMessageCommand(subjectId, SubjectCategory.Cloud, "hello");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ACCESS_DENIED");
    }
}
