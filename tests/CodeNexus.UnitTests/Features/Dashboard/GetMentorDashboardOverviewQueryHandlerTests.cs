using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Dashboard.Queries.GetMentorDashboardOverview;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.Dashboard;

public class GetMentorDashboardOverviewQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly GetMentorDashboardOverviewQueryHandler _handler;

    public GetMentorDashboardOverviewQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new GetMentorDashboardOverviewQueryHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_UserIsNotMentor_ReturnsAccessDenied()
    {
        var userId = NewId.NextGuid();
        var role = new Role { RoleId = NewId.NextGuid(), RoleName = "Student" };
        var user = new User { UserId = userId, Username = "u1", Email = "u1@test.com", Role = role };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetMentorDashboardOverviewQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("ACCESS_DENIED");
    }

    [Fact]
    public async Task Handle_MentorWithData_ReturnsOverview()
    {
        var mentorId = NewId.NextGuid();
        var student1Id = NewId.NextGuid();
        var student2Id = NewId.NextGuid();

        var mentorRole = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" };
        var mentor = new User { UserId = mentorId, Username = "mentor", Email = "mentor@test.com", Role = mentorRole };
        var student1 = new User { UserId = student1Id, Username = "student1", Email = "s1@test.com" };
        var student2 = new User { UserId = student2Id, Username = "student2", Email = "s2@test.com" };

        var conversation1 = new DirectConversation
        {
            ConversationId = NewId.NextGuid(),
            MentorId = mentorId,
            StudentId = student1Id,
            Student = student1,
            ConversationType = ChatConversationType.Direct,
            Messages = new List<DirectMessage>()
        };

        var conversation2 = new DirectConversation
        {
            ConversationId = NewId.NextGuid(),
            MentorId = mentorId,
            StudentId = student2Id,
            Student = student2,
            ConversationType = ChatConversationType.Direct,
            Messages = new List<DirectMessage>()
        };

        var recentMessage1 = new DirectMessage
        {
            MessageId = NewId.NextGuid(),
            ConversationId = conversation1.ConversationId,
            Conversation = conversation1,
            SenderId = student1Id,
            Content = "msg1",
            MessageType = DirectMessageType.Text,
            SentAt = DateTime.UtcNow.AddMinutes(-5)
        };

        var recentMessage2 = new DirectMessage
        {
            MessageId = NewId.NextGuid(),
            ConversationId = conversation2.ConversationId,
            Conversation = conversation2,
            SenderId = student2Id,
            Content = "msg2",
            MessageType = DirectMessageType.Text,
            SentAt = DateTime.UtcNow.AddMinutes(-2)
        };

        conversation1.Messages.Add(recentMessage1);
        conversation2.Messages.Add(recentMessage2);

        var subjects = new List<Subject>
        {
            new() { SubjectId = NewId.NextGuid(), CreatedByUserId = mentorId, Name = "Math", IsDeleted = false },
            new() { SubjectId = NewId.NextGuid(), CreatedByUserId = mentorId, Name = "Physics", IsDeleted = false }
        };

        var learningPaths = new List<LearningPath>
        {
            new() { PathId = NewId.NextGuid(), UserId = mentorId, SubjectId = subjects[0].SubjectId, Title = "Draft 1", Status = LearningPathStatus.Draft.ToString() },
            new() { PathId = NewId.NextGuid(), UserId = mentorId, SubjectId = subjects[1].SubjectId, Title = "Active 1", Status = LearningPathStatus.Active.ToString() }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.Users).Returns(new[] { mentor }.AsQueryable().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.DirectConversations).Returns(new[] { conversation1, conversation2 }.AsQueryable().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Subjects).Returns(subjects.AsQueryable().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(learningPaths.AsQueryable().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.DirectMessages).Returns(new[] { recentMessage1, recentMessage2 }.AsQueryable().BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetMentorDashboardOverviewQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.SupportedStudentsCount.Should().Be(2);
        result.Value.CreatedSubjectsCount.Should().Be(2);
        result.Value.DraftLearningPathsCount.Should().Be(1);
        result.Value.RecentStudentMessages.Should().HaveCount(2);
        result.Value.RecentStudentMessages[0].StudentId.Should().Be(student2Id);
    }
}
