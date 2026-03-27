using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.LearningPathShares.Queries.GetSentLearningPathShares;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;

namespace CodeNexus.UnitTests.Features.LearningPathShares;

public class GetSentLearningPathSharesQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly GetSentLearningPathSharesQueryHandler _handler;

    public GetSentLearningPathSharesQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new GetSentLearningPathSharesQueryHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_MentorInDirectChat_ReturnsStatus()
    {
        var mentorId = NewId.NextGuid();
        var studentId = NewId.NextGuid();

        var mentor = new User
        {
            UserId = mentorId,
            Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" }
        };

        var student = new User
        {
            UserId = studentId,
            Username = "student-1",
            Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Student" }
        };

        var acceptedShare = new LearningPathShare
        {
            ShareId = NewId.NextGuid(),
            PathId = NewId.NextGuid(),
            MentorId = mentorId,
            StudentId = studentId,
            Status = LearningPathShareStatus.Accepted,
            SentAt = DateTime.UtcNow,
            RespondedAt = DateTime.UtcNow,
            Student = student,
            LearningPath = new LearningPath { PathId = NewId.NextGuid(), Title = "Accepted path", UserId = mentorId, SubjectId = NewId.NextGuid() }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.Users).Returns(new List<User> { mentor }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPathShares).Returns(new List<LearningPathShare> { acceptedShare }.BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetSentLearningPathSharesQuery(LearningPathShareStatus.Accepted, studentId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].Status.Should().Be(LearningPathShareStatus.Accepted);
        result.Value[0].StudentName.Should().Be("student-1");
    }

    [Fact]
    public async Task Handle_MentorInChannelContext_HidesStatus()
    {
        var mentorId = NewId.NextGuid();
        var studentId = NewId.NextGuid();

        var mentor = new User
        {
            UserId = mentorId,
            Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" }
        };

        var share = new LearningPathShare
        {
            ShareId = NewId.NextGuid(),
            PathId = NewId.NextGuid(),
            MentorId = mentorId,
            StudentId = studentId,
            Status = LearningPathShareStatus.Pending,
            SentAt = DateTime.UtcNow,
            Student = new User { UserId = studentId, Username = "student-1" },
            LearningPath = new LearningPath { PathId = NewId.NextGuid(), Title = "Path", UserId = mentorId, SubjectId = NewId.NextGuid() }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.Users).Returns(new List<User> { mentor }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPathShares).Returns(new List<LearningPathShare> { share }.BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetSentLearningPathSharesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].Status.Should().BeNull();
        result.Value[0].RespondedAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Student_ReturnsStatus()
    {
        var mentorId = NewId.NextGuid();
        var studentId = NewId.NextGuid();

        var student = new User
        {
            UserId = studentId,
            Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Student" }
        };

        var share = new LearningPathShare
        {
            ShareId = NewId.NextGuid(),
            PathId = NewId.NextGuid(),
            MentorId = mentorId,
            StudentId = studentId,
            Status = LearningPathShareStatus.Pending,
            SentAt = DateTime.UtcNow,
            Student = new User { UserId = studentId, Username = "student-1" },
            LearningPath = new LearningPath { PathId = NewId.NextGuid(), Title = "Path", UserId = mentorId, SubjectId = NewId.NextGuid() }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        _mockContext.Setup(x => x.Users).Returns(new List<User> { student }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPathShares).Returns(new List<LearningPathShare> { share }.BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetSentLearningPathSharesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].Status.Should().Be(LearningPathShareStatus.Pending);
    }
}
