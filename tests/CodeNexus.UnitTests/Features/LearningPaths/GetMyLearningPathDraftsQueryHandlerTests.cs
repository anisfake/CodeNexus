using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.LearningPathSkeleton.Queries.GetMyLearningPathDrafts;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;
using GoalEntity = CodeNexus.Domain.Entities.Goals;

namespace CodeNexus.UnitTests.Features.LearningPaths;

public class GetMyLearningPathDraftsQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly GetMyLearningPathDraftsQueryHandler _handler;

    public GetMyLearningPathDraftsQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new GetMyLearningPathDraftsQueryHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_MentorHasDrafts_ReturnsOnlyDraftLearningPaths()
    {
        var mentorId = NewId.NextGuid();
        var role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" };
        var mentor = new User { UserId = mentorId, Username = "mentor", Role = role };
        var subject = new Subject { SubjectId = NewId.NextGuid(), Name = "C#", CreatedByUserId = mentorId };
        var goal = new GoalEntity { GoalId = NewId.NextGuid(), Title = "Goal", Duration = GoalDuration.OneWeek, IsSystemDefined = false};

        var draft = new LearningPath
        {
            PathId = NewId.NextGuid(),
            UserId = mentorId,
            SubjectId = subject.SubjectId,
            Subject = subject,
            User = mentor,
            Title = "Draft path",
            Status = LearningPathStatus.Draft.ToString(),
            LearningPathGoals = new List<LearningPathGoal>
            {
                new() { PathId = NewId.NextGuid(), GoalId = goal.GoalId, Goal = goal, Weight = 100 }
            },
            Chapters = new List<Chapter>()
        };

        var active = new LearningPath
        {
            PathId = NewId.NextGuid(),
            UserId = mentorId,
            SubjectId = subject.SubjectId,
            Subject = subject,
            User = mentor,
            Title = "Active path",
            Status = LearningPathStatus.Active.ToString(),
            LearningPathGoals = new List<LearningPathGoal>(),
            Chapters = new List<Chapter>()
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.Users).Returns(new List<User> { mentor }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath> { draft, active }.BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetMyLearningPathDraftsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items[0].Status.Should().Be(LearningPathStatus.Draft.ToString());
        result.Value.Items[0].Title.Should().Be("Draft path");
    }

    [Fact]
    public async Task Handle_NonMentorUser_ReturnsAccessDenied()
    {
        var userId = NewId.NextGuid();
        var user = new User
        {
            UserId = userId,
            Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Student" }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Users).Returns(new List<User> { user }.BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetMyLearningPathDraftsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ACCESS_DENIED");
    }

    [Fact]
    public async Task Handle_MultipleStatuses_ReturnsOnlyDrafts()
    {
        var mentorId = NewId.NextGuid();
        var role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" };
        var mentor = new User { UserId = mentorId, Username = "mentor", Role = role };
        var subject = new Subject { SubjectId = NewId.NextGuid(), Name = "C#", CreatedByUserId = mentorId };

        var draft1 = new LearningPath
        {
            PathId = NewId.NextGuid(), UserId = mentorId, SubjectId = subject.SubjectId,
            Subject = subject, User = mentor, Title = "Draft 1",
            Status = LearningPathStatus.Draft.ToString(),
            LearningPathGoals = new List<LearningPathGoal>(), Chapters = new List<Chapter>()
        };
        var draft2 = new LearningPath
        {
            PathId = NewId.NextGuid(), UserId = mentorId, SubjectId = subject.SubjectId,
            Subject = subject, User = mentor, Title = "Draft 2",
            Status = LearningPathStatus.Draft.ToString(),
            LearningPathGoals = new List<LearningPathGoal>(), Chapters = new List<Chapter>()
        };
        var published = new LearningPath
        {
            PathId = NewId.NextGuid(), UserId = mentorId, SubjectId = subject.SubjectId,
            Subject = subject, User = mentor, Title = "Published path",
            Status = LearningPathStatus.Published.ToString(),
            LearningPathGoals = new List<LearningPathGoal>(), Chapters = new List<Chapter>()
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.Users).Returns(new List<User> { mentor }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(
            new List<LearningPath> { draft1, draft2, published }.BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetMyLearningPathDraftsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.Items.Should().OnlyContain(i => i.Status == LearningPathStatus.Draft.ToString());
    }
}
