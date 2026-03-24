using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.LearningPathShares.Commands.SendLearningPathShare;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;

namespace CodeNexus.UnitTests.Features.LearningPathShares;

public class SendLearningPathShareCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly SendLearningPathShareCommandHandler _handler;

    public SendLearningPathShareCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new SendLearningPathShareCommandHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_DraftPath_ActivatesPathAndCreatesShareMessage()
    {
        var mentorId = NewId.NextGuid();
        var studentId = NewId.NextGuid();
        var pathId = NewId.NextGuid();

        var mentor = new User
        {
            UserId = mentorId,
            Username = "mentor",
            Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" }
        };

        var student = new User
        {
            UserId = studentId,
            Username = "student",
            Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Student" }
        };

        var learningPath = new LearningPath
        {
            PathId = pathId,
            UserId = mentorId,
            SubjectId = NewId.NextGuid(),
            Title = "Draft LP",
            Status = LearningPathStatus.Draft.ToString()
        };

        var usersDbSet = new List<User> { mentor, student }.BuildMockDbSet();
        var pathsDbSet = new List<LearningPath> { learningPath }.BuildMockDbSet();

        var shares = new List<LearningPathShare>();
        var sharesDbSet = shares.BuildMockDbSet();
        sharesDbSet.Setup(x => x.Add(It.IsAny<LearningPathShare>()))
            .Callback<LearningPathShare>(shares.Add);

        var conversations = new List<DirectConversation>();
        var conversationsDbSet = conversations.BuildMockDbSet();
        conversationsDbSet.Setup(x => x.Add(It.IsAny<DirectConversation>()))
            .Callback<DirectConversation>(conversations.Add);

        var messages = new List<DirectMessage>();
        var messagesDbSet = messages.BuildMockDbSet();
        messagesDbSet.Setup(x => x.Add(It.IsAny<DirectMessage>()))
            .Callback<DirectMessage>(messages.Add);

        var receipts = new List<DirectMessageReceipt>();
        var receiptsDbSet = receipts.BuildMockDbSet();
        receiptsDbSet.Setup(x => x.Add(It.IsAny<DirectMessageReceipt>()))
            .Callback<DirectMessageReceipt>(receipts.Add);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.Users).Returns(usersDbSet.Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(pathsDbSet.Object);
        _mockContext.Setup(x => x.LearningPathShares).Returns(sharesDbSet.Object);
        _mockContext.Setup(x => x.DirectConversations).Returns(conversationsDbSet.Object);
        _mockContext.Setup(x => x.DirectMessages).Returns(messagesDbSet.Object);
        _mockContext.Setup(x => x.DirectMessageReceipts).Returns(receiptsDbSet.Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(new SendLearningPathShareCommand(pathId, studentId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        learningPath.Status.Should().Be(LearningPathStatus.Active.ToString());
        shares.Should().HaveCount(1);
        messages.Should().HaveCount(1);
        messages[0].MessageType.Should().Be(DirectMessageType.LearningPathShare);
        messages[0].LearningPathShareId.Should().Be(shares[0].ShareId);
        receipts.Should().HaveCount(1);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingPendingShare_ReturnsConflict()
    {
        var mentorId = NewId.NextGuid();
        var studentId = NewId.NextGuid();
        var pathId = NewId.NextGuid();

        var mentor = new User
        {
            UserId = mentorId,
            Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" }
        };

        var student = new User
        {
            UserId = studentId,
            Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Student" }
        };

        var existingShare = new LearningPathShare
        {
            ShareId = NewId.NextGuid(),
            PathId = pathId,
            MentorId = mentorId,
            StudentId = studentId,
            Status = LearningPathShareStatus.Pending,
            SentAt = DateTime.UtcNow
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.Users).Returns(new List<User> { mentor, student }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath>
        {
            new() { PathId = pathId, UserId = mentorId, SubjectId = NewId.NextGuid(), Title = "Path", Status = LearningPathStatus.Active.ToString() }
        }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPathShares).Returns(new List<LearningPathShare> { existingShare }.BuildMockDbSet().Object);

        var result = await _handler.Handle(new SendLearningPathShareCommand(pathId, studentId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("SHARE_ALREADY_PENDING");
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
