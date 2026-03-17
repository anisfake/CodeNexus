using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.DirectChats.Commands.CreateDirectConversation;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;

namespace CodeNexus.UnitTests.Features.DirectChats;

public class CreateDirectConversationCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly CreateDirectConversationCommandHandler _handler;

    public CreateDirectConversationCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new CreateDirectConversationCommandHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_ValidMentorStudentPair_CreatesConversation()
    {
        var mentorId = NewId.NextGuid();
        var studentId = NewId.NextGuid();

        var users = new List<User>
        {
            new() { UserId = mentorId, Username = "mentor-1", Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" } },
            new() { UserId = studentId, Username = "student-1", Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Student" } }
        };

        var directConversationsMock = new List<DirectConversation>().BuildMockDbSet();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.Users).Returns(users.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.DirectConversations).Returns(directConversationsMock.Object);
        _mockContext.Setup(x => x.DirectMessageReceipts).Returns(new List<DirectMessageReceipt>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new CreateDirectConversationCommand(studentId);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.MentorId.Should().Be(mentorId);
        result.Value.StudentId.Should().Be(studentId);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SameRolePair_ReturnsFailure()
    {
        var mentorId = NewId.NextGuid();
        var anotherMentorId = NewId.NextGuid();

        var users = new List<User>
        {
            new() { UserId = mentorId, Username = "mentor-1", Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" } },
            new() { UserId = anotherMentorId, Username = "mentor-2", Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" } }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.Users).Returns(users.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.DirectConversations).Returns(new List<DirectConversation>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.DirectMessageReceipts).Returns(new List<DirectMessageReceipt>().BuildMockDbSet().Object);

        var command = new CreateDirectConversationCommand(anotherMentorId);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_PARTICIPANTS");
    }
}
