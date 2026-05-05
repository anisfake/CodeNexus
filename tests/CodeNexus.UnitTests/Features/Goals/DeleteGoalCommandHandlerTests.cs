using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Goals.Commands.DeleteGoal;
using CodeNexus.UnitTests.Helpers;
using GoalEntity = CodeNexus.Domain.Entities.Goals;
using Moq;
using Xunit;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.UnitTests.Features.Goals;

public class DeleteGoalCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly DeleteGoalCommandHandler _handler;
    private readonly Guid _testUserId = Guid.NewGuid();

    public DeleteGoalCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(_testUserId);
        _handler = new DeleteGoalCommandHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_WhenGoalExists_SoftDeletesSuccessfully()
    {
        // Arrange
        var goalId = Guid.NewGuid();
        var existingGoal = new GoalEntity
        {
            GoalId = goalId,
            CreatedByUserId = _testUserId,
            Title = "Goal to Delete",
            IsSystemDefined = false,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        var goals = new List<GoalEntity> { existingGoal };
        SetupGoalsDbSet(goals);
        SetupLearningPathGoalsDbSet(new List<CodeNexus.Domain.Entities.LearningPathGoal>());
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new DeleteGoalCommand(goalId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Delete goal successful!", result.Value);
        Assert.True(existingGoal.IsDeleted);
        Assert.NotNull(existingGoal.DeletedAt);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenGoalNotFound_ReturnsFailure()
    {
        // Arrange
        var goalId = Guid.NewGuid();
        SetupGoalsDbSet(new List<GoalEntity>());
        SetupLearningPathGoalsDbSet(new List<CodeNexus.Domain.Entities.LearningPathGoal>());

        var command = new DeleteGoalCommand(goalId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("GOAL_NOT_FOUND", result.ErrorCode);
        Assert.Equal("Goal not found.", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WhenGoalBelongsToAnotherUser_ReturnsFailure()
    {
        // Arrange
        var goalId = Guid.NewGuid();
        var anotherUserId = Guid.NewGuid();
        var existingGoal = new GoalEntity
        {
            GoalId = goalId,
            CreatedByUserId = anotherUserId,
            Title = "Another User's Goal",
            IsSystemDefined = false,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        var goals = new List<GoalEntity> { existingGoal };
        SetupGoalsDbSet(goals);
        SetupLearningPathGoalsDbSet(new List<CodeNexus.Domain.Entities.LearningPathGoal>());

        var command = new DeleteGoalCommand(goalId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("GOAL_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task Handle_WhenDeletingSystemGoal_ReturnsFailure()
    {
        // Arrange
        var goalId = Guid.NewGuid();
        var systemGoal = new GoalEntity
        {
            GoalId = goalId,
            CreatedByUserId = null,
            Title = "System Goal",
            IsSystemDefined = true,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        var goals = new List<GoalEntity> { systemGoal };
        SetupGoalsDbSet(goals);
        SetupLearningPathGoalsDbSet(new List<CodeNexus.Domain.Entities.LearningPathGoal>());

        var command = new DeleteGoalCommand(goalId);

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
        var existingGoal = new GoalEntity
        {
            GoalId = goalId,
            CreatedByUserId = _testUserId,
            Title = "Goal in use",
            IsSystemDefined = false,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        SetupGoalsDbSet(new List<GoalEntity> { existingGoal });
        SetupLearningPathGoalsDbSet(new List<CodeNexus.Domain.Entities.LearningPathGoal>
        {
            new()
            {
                PathId = Guid.NewGuid(),
                GoalId = goalId,
                Weight = 1.0m
            }
        });

        var command = new DeleteGoalCommand(goalId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("GOAL_IN_USE", result.ErrorCode);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private void SetupLearningPathGoalsDbSet(List<CodeNexus.Domain.Entities.LearningPathGoal> learningPathGoals)
    {
        var queryable = new TestAsyncEnumerable<CodeNexus.Domain.Entities.LearningPathGoal>(learningPathGoals);
        var dbSetMock = new Mock<DbSet<CodeNexus.Domain.Entities.LearningPathGoal>>();
        dbSetMock.As<IQueryable<CodeNexus.Domain.Entities.LearningPathGoal>>().Setup(m => m.Provider).Returns(queryable.AsQueryable().Provider);
        dbSetMock.As<IQueryable<CodeNexus.Domain.Entities.LearningPathGoal>>().Setup(m => m.Expression).Returns(queryable.AsQueryable().Expression);
        dbSetMock.As<IQueryable<CodeNexus.Domain.Entities.LearningPathGoal>>().Setup(m => m.ElementType).Returns(queryable.AsQueryable().ElementType);
        dbSetMock.As<IQueryable<CodeNexus.Domain.Entities.LearningPathGoal>>().Setup(m => m.GetEnumerator()).Returns(queryable.AsQueryable().GetEnumerator());
        dbSetMock.As<IAsyncEnumerable<CodeNexus.Domain.Entities.LearningPathGoal>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(queryable.GetAsyncEnumerator());
        _mockContext.Setup(x => x.LearningPathGoals).Returns(dbSetMock.Object);
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
}
