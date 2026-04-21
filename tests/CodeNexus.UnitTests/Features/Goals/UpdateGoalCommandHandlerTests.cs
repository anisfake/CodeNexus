using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Goals.Commands.UpdateGoal;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using CodeNexus.Domain.Entities;
using GoalEntity = CodeNexus.Domain.Entities.Goals;
using Moq;
using Xunit;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.UnitTests.Features.Goals;

public class UpdateGoalCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<IGoalValidationService> _mockGoalValidationService;
    private readonly UpdateGoalCommandHandler _handler;
    private readonly Guid _testUserId = Guid.NewGuid();

    public UpdateGoalCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockGoalValidationService = new Mock<IGoalValidationService>();
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(_testUserId);
        _handler = new UpdateGoalCommandHandler(_mockContext.Object, _mockCurrentUserService.Object, _mockGoalValidationService.Object);
    }

    [Fact]
    public async Task Handle_WhenGoalExists_UpdatesSuccessfully()
    {
        // Arrange
        var goalId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var existingGoal = new GoalEntity
        {
            GoalId = goalId,
            CreatedByUserId = _testUserId,
            Title = "Old Title",
            Description = "Old Description",
            IsSystemDefined = false,
            Duration = GoalDuration.OneMonth,
            CreatedAt = DateTime.UtcNow
        };

        var goals = new List<GoalEntity> { existingGoal };
        SetupGoalsDbSet(goals);
        SetupSubjectsDbSet(new List<Subject>
        {
            new()
            {
                SubjectId = subjectId,
                Name = "C#",
                Description = "C# language"
            }
        });
        SetupSubjectGoalsDbSet(new List<SubjectGoal>
        {
            new()
            {
                SubjectId = subjectId,
                GoalId = goalId
            }
        });
        SetupLearningPathGoalsDbSet(new List<LearningPathGoal>());
        SetupGoalMappingsDbSet(new List<GoalMapping>());
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _mockGoalValidationService.Setup(x => x.IsRelatedToProgrammingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockGoalValidationService.Setup(x => x.IsGoalRelevantToSubjectAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockGoalValidationService.Setup(x => x.FindBestSystemGoalMatchAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<IReadOnlyList<GoalMatchCandidate>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoalMatchResult(null, null));

        var command = new UpdateGoalCommand(
            goalId,
            subjectId,
            "New Title",
            "New Description",
            GoalDuration.TwoMonths
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("New Title", result.Value.Title);
        Assert.Equal("New Description", result.Value.Description);
        Assert.Equal(GoalDuration.TwoMonths, result.Value.Duration);
    }

    [Fact]
    public async Task Handle_WhenGoalNotFound_ReturnsFailure()
    {
        // Arrange
        var goalId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupGoalsDbSet(new List<GoalEntity>());
        SetupSubjectsDbSet(new List<Subject>
        {
            new()
            {
                SubjectId = subjectId,
                Name = "C#",
                Description = "C# language"
            }
        });
        SetupSubjectGoalsDbSet(new List<SubjectGoal>());
        SetupLearningPathGoalsDbSet(new List<LearningPathGoal>());

        var command = new UpdateGoalCommand(
            goalId,
            subjectId,
            "New Title",
            "New Description",
            GoalDuration.OneMonth
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("GOAL_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task Handle_WhenGoalBelongsToAnotherUser_ReturnsFailure()
    {
        // Arrange
        var goalId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var anotherUserId = Guid.NewGuid();
        var existingGoal = new GoalEntity
        {
            GoalId = goalId,
            CreatedByUserId = anotherUserId,
            Title = "Old Title",
            IsSystemDefined = false,
            Duration = GoalDuration.OneMonth,
            CreatedAt = DateTime.UtcNow
        };

        var goals = new List<GoalEntity> { existingGoal };
        SetupGoalsDbSet(goals);
        SetupSubjectsDbSet(new List<Subject>
        {
            new()
            {
                SubjectId = subjectId,
                Name = "C#",
                Description = "C# language"
            }
        });
        SetupSubjectGoalsDbSet(new List<SubjectGoal>
        {
            new()
            {
                SubjectId = subjectId,
                GoalId = goalId
            }
        });
        SetupLearningPathGoalsDbSet(new List<LearningPathGoal>());
        SetupGoalMappingsDbSet(new List<GoalMapping>());

        var command = new UpdateGoalCommand(goalId, subjectId, "New Title", null, GoalDuration.TwoMonths);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("GOAL_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task Handle_WhenUpdatingSystemGoal_ReturnsFailure()
    {
        // Arrange
        var goalId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var existingGoal = new GoalEntity
        {
            GoalId = goalId,
            CreatedByUserId = null,
            Title = "System Goal",
            IsSystemDefined = true,
            Duration = GoalDuration.OneMonth,
            CreatedAt = DateTime.UtcNow
        };

        var goals = new List<GoalEntity> { existingGoal };
        SetupGoalsDbSet(goals);
        SetupSubjectsDbSet(new List<Subject>
        {
            new()
            {
                SubjectId = subjectId,
                Name = "C#",
                Description = "C# language"
            }
        });
        SetupSubjectGoalsDbSet(new List<SubjectGoal>
        {
            new()
            {
                SubjectId = subjectId,
                GoalId = goalId
            }
        });
        SetupLearningPathGoalsDbSet(new List<LearningPathGoal>());
        SetupGoalMappingsDbSet(new List<GoalMapping>());

        var command = new UpdateGoalCommand(goalId, subjectId, "Updated Title", null, GoalDuration.ThreeMonths);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("GOAL_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task Handle_WhenGoalInLearningPath_ReturnsFailure()
    {
        // Arrange
        var goalId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var existingGoal = new GoalEntity
        {
            GoalId = goalId,
            CreatedByUserId = _testUserId,
            Title = "Personal Goal",
            Description = "Description",
            IsSystemDefined = false,
            Duration = GoalDuration.OneMonth,
            CreatedAt = DateTime.UtcNow
        };

        SetupGoalsDbSet(new List<GoalEntity> { existingGoal });
        SetupSubjectsDbSet(new List<Subject>
        {
            new()
            {
                SubjectId = subjectId,
                Name = "C#",
                Description = "C# language"
            }
        });
        SetupSubjectGoalsDbSet(new List<SubjectGoal>
        {
            new()
            {
                SubjectId = subjectId,
                GoalId = goalId
            }
        });
        SetupLearningPathGoalsDbSet(new List<LearningPathGoal>
        {
            new()
            {
                PathId = Guid.NewGuid(),
                GoalId = goalId,
                Weight = 1.0m
            }
        });
        SetupGoalMappingsDbSet(new List<GoalMapping>());

        var command = new UpdateGoalCommand(goalId, subjectId, "Updated Goal", "Updated Description", GoalDuration.TwoMonths);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("GOAL_IN_USE", result.ErrorCode);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private void SetupGoalsDbSet(List<GoalEntity> goals)
    {
        var queryable = new TestAsyncEnumerable<GoalEntity>(goals);
        var dbSetMock = new Mock<DbSet<GoalEntity>>();
        dbSetMock.As<IQueryable<GoalEntity>>().Setup(m => m.Provider).Returns(queryable.AsQueryable().Provider);
        dbSetMock.As<IQueryable<GoalEntity>>().Setup(m => m.Expression).Returns(queryable.AsQueryable().Expression);
        dbSetMock.As<IQueryable<GoalEntity>>().Setup(m => m.ElementType).Returns(queryable.AsQueryable().ElementType);
        dbSetMock.As<IQueryable<GoalEntity>>().Setup(m => m.GetEnumerator()).Returns(queryable.AsQueryable().GetEnumerator());
        dbSetMock.As<IAsyncEnumerable<GoalEntity>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(queryable.GetAsyncEnumerator());
        _mockContext.Setup(x => x.Goals).Returns(dbSetMock.Object);
    }

    private void SetupSubjectsDbSet(List<Subject> subjects)
    {
        var queryable = new TestAsyncEnumerable<Subject>(subjects);
        var dbSetMock = new Mock<DbSet<Subject>>();
        dbSetMock.As<IQueryable<Subject>>().Setup(m => m.Provider).Returns(queryable.AsQueryable().Provider);
        dbSetMock.As<IQueryable<Subject>>().Setup(m => m.Expression).Returns(queryable.AsQueryable().Expression);
        dbSetMock.As<IQueryable<Subject>>().Setup(m => m.ElementType).Returns(queryable.AsQueryable().ElementType);
        dbSetMock.As<IQueryable<Subject>>().Setup(m => m.GetEnumerator()).Returns(queryable.AsQueryable().GetEnumerator());
        dbSetMock.As<IAsyncEnumerable<Subject>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(queryable.GetAsyncEnumerator());
        _mockContext.Setup(x => x.Subjects).Returns(dbSetMock.Object);
    }

    private void SetupSubjectGoalsDbSet(List<SubjectGoal> subjectGoals)
    {
        var queryable = new TestAsyncEnumerable<SubjectGoal>(subjectGoals);
        var dbSetMock = new Mock<DbSet<SubjectGoal>>();
        dbSetMock.As<IQueryable<SubjectGoal>>().Setup(m => m.Provider).Returns(queryable.AsQueryable().Provider);
        dbSetMock.As<IQueryable<SubjectGoal>>().Setup(m => m.Expression).Returns(queryable.AsQueryable().Expression);
        dbSetMock.As<IQueryable<SubjectGoal>>().Setup(m => m.ElementType).Returns(queryable.AsQueryable().ElementType);
        dbSetMock.As<IQueryable<SubjectGoal>>().Setup(m => m.GetEnumerator()).Returns(queryable.AsQueryable().GetEnumerator());
        dbSetMock.As<IAsyncEnumerable<SubjectGoal>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(queryable.GetAsyncEnumerator());
        _mockContext.Setup(x => x.SubjectGoals).Returns(dbSetMock.Object);
    }

    private void SetupGoalMappingsDbSet(List<GoalMapping> goalMappings)
    {
        var queryable = new TestAsyncEnumerable<GoalMapping>(goalMappings);
        var dbSetMock = new Mock<DbSet<GoalMapping>>();
        dbSetMock.As<IQueryable<GoalMapping>>().Setup(m => m.Provider).Returns(queryable.AsQueryable().Provider);
        dbSetMock.As<IQueryable<GoalMapping>>().Setup(m => m.Expression).Returns(queryable.AsQueryable().Expression);
        dbSetMock.As<IQueryable<GoalMapping>>().Setup(m => m.ElementType).Returns(queryable.AsQueryable().ElementType);
        dbSetMock.As<IQueryable<GoalMapping>>().Setup(m => m.GetEnumerator()).Returns(queryable.AsQueryable().GetEnumerator());
        dbSetMock.As<IAsyncEnumerable<GoalMapping>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(queryable.GetAsyncEnumerator());
        _mockContext.Setup(x => x.GoalMappings).Returns(dbSetMock.Object);
    }

    private void SetupLearningPathGoalsDbSet(List<LearningPathGoal> learningPathGoals)
    {
        var queryable = new TestAsyncEnumerable<LearningPathGoal>(learningPathGoals);
        var dbSetMock = new Mock<DbSet<LearningPathGoal>>();
        dbSetMock.As<IQueryable<LearningPathGoal>>().Setup(m => m.Provider).Returns(queryable.AsQueryable().Provider);
        dbSetMock.As<IQueryable<LearningPathGoal>>().Setup(m => m.Expression).Returns(queryable.AsQueryable().Expression);
        dbSetMock.As<IQueryable<LearningPathGoal>>().Setup(m => m.ElementType).Returns(queryable.AsQueryable().ElementType);
        dbSetMock.As<IQueryable<LearningPathGoal>>().Setup(m => m.GetEnumerator()).Returns(queryable.AsQueryable().GetEnumerator());
        dbSetMock.As<IAsyncEnumerable<LearningPathGoal>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(queryable.GetAsyncEnumerator());
        _mockContext.Setup(x => x.LearningPathGoals).Returns(dbSetMock.Object);
    }
}
