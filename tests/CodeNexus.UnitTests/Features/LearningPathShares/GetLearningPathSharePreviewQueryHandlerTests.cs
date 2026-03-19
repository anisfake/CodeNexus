using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.LearningPathShares.Queries.GetLearningPathSharePreview;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;
using GoalEntity = CodeNexus.Domain.Entities.Goals;

namespace CodeNexus.UnitTests.Features.LearningPathShares;

public class GetLearningPathSharePreviewQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly GetLearningPathSharePreviewQueryHandler _handler;

    public GetLearningPathSharePreviewQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new GetLearningPathSharePreviewQueryHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_ValidStudentShare_ReturnsPreview()
    {
        var mentorId = NewId.NextGuid();
        var studentId = NewId.NextGuid();
        var shareId = NewId.NextGuid();
        var pathId = NewId.NextGuid();
        var subjectId = NewId.NextGuid();

        var mentor = new User
        {
            UserId = mentorId,
            Username = "mentor-1",
            Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" }
        };

        var student = new User
        {
            UserId = studentId,
            Username = "student-1",
            Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Student" }
        };

        var share = new LearningPathShare
        {
            ShareId = shareId,
            PathId = pathId,
            MentorId = mentorId,
            StudentId = studentId,
            Status = LearningPathShareStatus.Pending,
            SentAt = DateTime.UtcNow,
            Mentor = mentor,
            Student = student
        };

        var goalId = NewId.NextGuid();
        var learningPath = new LearningPath
        {
            PathId = pathId,
            UserId = mentorId,
            User = mentor,
            SubjectId = subjectId,
            Subject = new Subject { SubjectId = subjectId, Name = "C#", CreatedByUserId = mentorId },
            Title = "C# Path",
            Description = "desc",
            Status = LearningPathStatus.Active.ToString(),
            LearningPathGoals = new List<LearningPathGoal>
            {
                new() { PathId = pathId, GoalId = goalId, Weight = 100, Goal = new GoalEntity { GoalId = goalId, Title = "Goal", Duration = GoalDuration.OneWeek } }
            },
            Chapters = new List<Chapter>()
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        _mockContext.Setup(x => x.Users).Returns(new List<User> { student }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPathShares).Returns(new List<LearningPathShare> { share }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath> { learningPath }.BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetLearningPathSharePreviewQuery(shareId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.ShareId.Should().Be(shareId);
        result.Value.LearningPath.PathId.Should().Be(pathId);
        result.Value.MentorName.Should().Be("mentor-1");
    }

    [Fact]
    public async Task Handle_NonStudentUser_ReturnsAccessDenied()
    {
        var mentorId = NewId.NextGuid();

        var mentor = new User
        {
            UserId = mentorId,
            Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.Users).Returns(new List<User> { mentor }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPathShares).Returns(new List<LearningPathShare>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath>().BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetLearningPathSharePreviewQuery(NewId.NextGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ACCESS_DENIED");
    }
}
