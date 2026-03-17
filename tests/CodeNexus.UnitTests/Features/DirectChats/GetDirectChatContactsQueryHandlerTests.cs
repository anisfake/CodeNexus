using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.DirectChats.Queries.GetDirectChatContacts;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;

namespace CodeNexus.UnitTests.Features.DirectChats;

public class GetDirectChatContactsQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly GetDirectChatContactsQueryHandler _handler;

    public GetDirectChatContactsQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new GetDirectChatContactsQueryHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_CurrentUserIsStudent_ReturnsMentorList()
    {
        var studentId = NewId.NextGuid();
        var mentor1Id = NewId.NextGuid();
        var mentor2Id = NewId.NextGuid();

        var users = new List<User>
        {
            new() { UserId = studentId, Username = "student-1", Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Student" } },
            new() { UserId = mentor1Id, Username = "mentor-1", Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" } },
            new() { UserId = mentor2Id, Username = "mentor-2", Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" } }
        };

        var conversations = new List<DirectConversation>
        {
            new()
            {
                ConversationId = NewId.NextGuid(),
                MentorId = mentor1Id,
                StudentId = studentId,
                LastMessageAt = DateTime.UtcNow
            }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        _mockContext.Setup(x => x.Users).Returns(users.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.DirectConversations).Returns(conversations.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.DirectMessages).Returns(new List<DirectMessage>().BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetDirectChatContactsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(2);
        result.Value!.Select(x => x.UserId).Should().Contain(new[] { mentor1Id, mentor2Id });
        result.Value.First(x => x.UserId == mentor1Id).ConversationId.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_CurrentUserIsMentor_ReturnsOnlyStudentsWhoMessaged()
    {
        var mentorId = NewId.NextGuid();
        var student1Id = NewId.NextGuid();
        var student2Id = NewId.NextGuid();

        var mentor = new User
        {
            UserId = mentorId,
            Username = "mentor-1",
            Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" }
        };

        var student1 = new User
        {
            UserId = student1Id,
            Username = "student-1",
            Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Student" }
        };

        var student2 = new User
        {
            UserId = student2Id,
            Username = "student-2",
            Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Student" }
        };

        var conversation1 = new DirectConversation
        {
            ConversationId = NewId.NextGuid(),
            MentorId = mentorId,
            StudentId = student1Id,
            Student = student1,
            LastMessageAt = DateTime.UtcNow
        };

        var conversation2 = new DirectConversation
        {
            ConversationId = NewId.NextGuid(),
            MentorId = mentorId,
            StudentId = student2Id,
            Student = student2,
            LastMessageAt = DateTime.UtcNow.AddMinutes(-10)
        };

        var messages = new List<DirectMessage>
        {
            new()
            {
                MessageId = NewId.NextGuid(),
                ConversationId = conversation1.ConversationId,
                SenderId = student1Id,
                Content = "hi",
                SentAt = DateTime.UtcNow
            },
            new()
            {
                MessageId = NewId.NextGuid(),
                ConversationId = conversation2.ConversationId,
                SenderId = mentorId,
                Content = "hello",
                SentAt = DateTime.UtcNow
            }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.Users).Returns(new List<User> { mentor, student1, student2 }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.DirectConversations).Returns(new List<DirectConversation> { conversation1, conversation2 }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.DirectMessages).Returns(messages.BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetDirectChatContactsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].UserId.Should().Be(student1Id);
    }
}
