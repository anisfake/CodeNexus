using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.LearningPathShares.Queries.GetPendingLearningPathShares;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;

namespace CodeNexus.UnitTests.Features.LearningPathShares;

public class GetPendingLearningPathSharesQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly GetPendingLearningPathSharesQueryHandler _handler;

    public GetPendingLearningPathSharesQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new GetPendingLearningPathSharesQueryHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_StudentHasPendingShares_ReturnsOnlyPending()
    {
        var studentId = NewId.NextGuid();

        var student = new User
        {
            UserId = studentId,
            Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Student" }
        };

        var mentor = new User
        {
            UserId = NewId.NextGuid(),
            Username = "mentor-1",
            Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" }
        };

        var pending = new LearningPathShare
        {
            ShareId = NewId.NextGuid(),
            StudentId = studentId,
            MentorId = mentor.UserId,
            PathId = NewId.NextGuid(),
            Status = LearningPathShareStatus.Pending,
            SnapshotTitle = "C# Path",
            SentAt = DateTime.UtcNow,
            Mentor = mentor,
            LearningPath = new LearningPath { PathId = NewId.NextGuid(), Title = "C# Path", Description = "desc", UserId = mentor.UserId, SubjectId = NewId.NextGuid() }
        };

        var accepted = new LearningPathShare
        {
            ShareId = NewId.NextGuid(),
            StudentId = studentId,
            MentorId = mentor.UserId,
            PathId = NewId.NextGuid(),
            Status = LearningPathShareStatus.Accepted,
            SentAt = DateTime.UtcNow.AddMinutes(-10),
            Mentor = mentor,
            LearningPath = new LearningPath { PathId = NewId.NextGuid(), Title = "Other", UserId = mentor.UserId, SubjectId = NewId.NextGuid() }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        _mockContext.Setup(x => x.Users).Returns(new List<User> { student }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPathShares).Returns(new List<LearningPathShare> { pending, accepted }.BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetPendingLearningPathSharesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].ShareId.Should().Be(pending.ShareId);
        result.Value[0].LearningPathTitle.Should().Be("C# Path");
    }
}
