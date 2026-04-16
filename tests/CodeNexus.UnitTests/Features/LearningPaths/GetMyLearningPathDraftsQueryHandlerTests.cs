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
    public async Task Handle_DeletedTask_ExcludedFromResponse()
    {
        var mentorId = NewId.NextGuid();
        var chapterId = NewId.NextGuid();
        var role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" };
        var mentor = new User { UserId = mentorId, Username = "mentor", Role = role };
        var subject = new Subject { SubjectId = NewId.NextGuid(), Name = "C#", CreatedByUserId = mentorId };
        var goal = new GoalEntity { GoalId = NewId.NextGuid(), Title = "Goal", Duration = GoalDuration.OneWeek, IsSystemDefined = false};

        var activeTaskId = NewId.NextGuid();
        var deletedTaskId = NewId.NextGuid();

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
            Chapters = new List<Chapter>
            {
                new()
                {
                    ChapterId = chapterId,
                    PathId = NewId.NextGuid(),
                    Title = "Chapter 1",
                    OrderIndex = 0,
                    Lessons = new List<Lesson>(),
                    Tasks = new List<CodeNexus.Domain.Entities.Tasks>
                    {
                        new()
                        {
                            TaskId = activeTaskId,
                            ChapterId = chapterId,
                            PathId = NewId.NextGuid(),
                            Title = "Active task",
                            TaskType = TaskType.Practice,
                            Status = TaskStatus_.Pending,
                            IsDeleted = false
                        },
                        new()
                        {
                            TaskId = deletedTaskId,
                            ChapterId = chapterId,
                            PathId = NewId.NextGuid(),
                            Title = "Deleted task",
                            TaskType = TaskType.Practice,
                            Status = TaskStatus_.Pending,
                            IsDeleted = true
                        }
                    }
                }
            }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.Users).Returns(new List<User> { mentor }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath> { draft }.BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetMyLearningPathDraftsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var tasks = result.Value!.Items[0].ChapterDtos[0].Tasks;
        tasks.Should().HaveCount(1);
        tasks[0].TaskId.Should().Be(activeTaskId);
    }
}
