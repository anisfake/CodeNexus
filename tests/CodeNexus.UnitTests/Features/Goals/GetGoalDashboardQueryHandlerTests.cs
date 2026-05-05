using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Goals.Queries.GetGoalDashboard;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;
using Xunit;
using GoalEntity = CodeNexus.Domain.Entities.Goals;

namespace CodeNexus.UnitTests.Features.Goals;

public class GetGoalDashboardQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly GetGoalDashboardQueryHandler _handler;

    public GetGoalDashboardQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new GetGoalDashboardQueryHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_DefaultStatusFilter_ReturnsPersonalGoalsAndActivePathGoalsOnly()
    {
        var userId = NewId.NextGuid();
        var otherUserId = NewId.NextGuid();
        var subjectId = NewId.NextGuid();
        var personalGoalId = NewId.NextGuid();
        var systemGoalId = NewId.NextGuid();
        var activePathId = NewId.NextGuid();
        var completedPathId = NewId.NextGuid();
        var now = DateTime.UtcNow;

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);

        var goals = new List<GoalEntity>
        {
            new()
            {
                GoalId = personalGoalId,
                Title = "Master C#",
                Description = "Personal C# goal",
                IsSystemDefined = false,
                CreatedByUserId = userId,
                CreatedAt = now
            },
            new()
            {
                GoalId = systemGoalId,
                Title = "Become Backend Developer",
                Description = "System goal",
                IsSystemDefined = true,
                CreatedAt = now
            },
            new()
            {
                GoalId = NewId.NextGuid(),
                Title = "Other user's goal",
                IsSystemDefined = false,
                CreatedByUserId = otherUserId,
                CreatedAt = now
            }
        };

        var subjects = new List<Subject>
        {
            new() { SubjectId = subjectId, Name = "C#" }
        };

        var paths = new List<LearningPath>
        {
            new()
            {
                PathId = activePathId,
                UserId = userId,
                SubjectId = subjectId,
                Title = "Active Path",
                Status = LearningPathStatus.Active.ToString(),
                CreatedAt = now
            },
            new()
            {
                PathId = completedPathId,
                UserId = userId,
                SubjectId = subjectId,
                Title = "Completed Path",
                Status = LearningPathStatus.Completed.ToString(),
                CreatedAt = now.AddMinutes(-1)
            }
        };

        var learningPathGoals = new List<LearningPathGoal>
        {
            new() { PathId = activePathId, GoalId = personalGoalId, Weight = 0.6m },
            new() { PathId = activePathId, GoalId = systemGoalId, Weight = 0.4m },
            new() { PathId = completedPathId, GoalId = personalGoalId, Weight = 1m }
        };

        var progressRows = new List<UserGoalProgress>
        {
            new()
            {
                UserGoalProgressId = NewId.NextGuid(),
                UserId = userId,
                GoalId = personalGoalId,
                LearningPathId = activePathId,
                Status = GoalProgressStatus.InProgress,
                ProgressPercent = 45m,
                LastUpdatedAt = now
            },
            new()
            {
                UserGoalProgressId = NewId.NextGuid(),
                UserId = userId,
                GoalId = systemGoalId,
                LearningPathId = activePathId,
                Status = GoalProgressStatus.InProgress,
                ProgressPercent = 20m,
                LastUpdatedAt = now
            },
            new()
            {
                UserGoalProgressId = NewId.NextGuid(),
                UserId = userId,
                GoalId = personalGoalId,
                LearningPathId = completedPathId,
                Status = GoalProgressStatus.Completed,
                ProgressPercent = 55m,
                LastUpdatedAt = now.AddMinutes(-2)
            }
        };

        _mockContext.Setup(x => x.Goals).Returns(goals.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Subjects).Returns(subjects.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(paths.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPathGoals).Returns(learningPathGoals.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.UserGoalProgresses).Returns(progressRows.BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetGoalDashboardQuery(PageNumber: 1, PageSize: 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();

        result.Value!.PersonalGoals.Should().HaveCount(1);
        result.Value.PersonalGoals[0].GoalId.Should().Be(personalGoalId);
        result.Value.PersonalGoals[0].ProgressPercent.Should().Be(100m);
        result.Value.PersonalGoals[0].Status.Should().Be(GoalProgressStatus.Completed.ToString());

        result.Value.PathGoals.TotalCount.Should().Be(2);
        result.Value.PathGoals.Items.Should().HaveCount(2);
        result.Value.PathGoals.Items.All(x => x.LearningPathId == activePathId).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenPathStatusIsCompleted_ReturnsOnlyCompletedPathGoals()
    {
        var userId = NewId.NextGuid();
        var subjectId = NewId.NextGuid();
        var goalId = NewId.NextGuid();
        var activePathId = NewId.NextGuid();
        var completedPathId = NewId.NextGuid();
        var now = DateTime.UtcNow;

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);

        _mockContext.Setup(x => x.Goals).Returns(new List<GoalEntity>
        {
            new() { GoalId = goalId, Title = "Custom", IsSystemDefined = false, CreatedByUserId = userId, CreatedAt = now }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.Subjects).Returns(new List<Subject>
        {
            new() { SubjectId = subjectId, Name = "C#" }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath>
        {
            new() { PathId = activePathId, UserId = userId, SubjectId = subjectId, Title = "Path A", Status = LearningPathStatus.Active.ToString(), CreatedAt = now },
            new() { PathId = completedPathId, UserId = userId, SubjectId = subjectId, Title = "Path B", Status = LearningPathStatus.Completed.ToString(), CreatedAt = now }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.LearningPathGoals).Returns(new List<LearningPathGoal>
        {
            new() { PathId = activePathId, GoalId = goalId, Weight = 1m },
            new() { PathId = completedPathId, GoalId = goalId, Weight = 1m }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.UserGoalProgresses).Returns(new List<UserGoalProgress>
        {
            new() { UserGoalProgressId = NewId.NextGuid(), UserId = userId, GoalId = goalId, LearningPathId = completedPathId, Status = GoalProgressStatus.InProgress, ProgressPercent = 20m, LastUpdatedAt = now }
        }.BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetGoalDashboardQuery(PathStatus: LearningPathStatus.Completed), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.PathGoals.TotalCount.Should().Be(1);
        result.Value.PathGoals.Items.Should().HaveCount(1);
        result.Value.PathGoals.Items[0].LearningPathId.Should().Be(completedPathId);
    }
}
