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
    public async Task Handle_WhenGoalExists_DeletesSuccessfully()
    {
        // Arrange
        var goalId = Guid.NewGuid();
        var existingGoal = new GoalEntity
        {
            GoalId = goalId,
            UserId = _testUserId,
            Title = "Goal to Delete",
            DurationDays = 30,
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow
        };

        var goals = new List<GoalEntity> { existingGoal };
        SetupGoalsDbSet(goals);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new DeleteGoalCommand(goalId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Delete goal successful!", result.Value);
        _mockContext.Verify(x => x.Goals.Remove(It.IsAny<GoalEntity>()), Times.Once);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenGoalNotFound_ReturnsFailure()
    {
        // Arrange
        var goalId = Guid.NewGuid();
        SetupGoalsDbSet(new List<GoalEntity>());

        var command = new DeleteGoalCommand(goalId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("GOAL_NOT_FOUND", result.ErrorCode);
        Assert.Equal("The specified goal was not found.", result.ErrorMessage);
        _mockContext.Verify(x => x.Goals.Remove(It.IsAny<GoalEntity>()), Times.Never);
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
            UserId = anotherUserId,
            Title = "Another User's Goal",
            DurationDays = 30,
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow
        };

        var goals = new List<GoalEntity> { existingGoal };
        SetupGoalsDbSet(goals);

        var command = new DeleteGoalCommand(goalId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("GOAL_NOT_FOUND", result.ErrorCode);
        _mockContext.Verify(x => x.Goals.Remove(It.IsAny<GoalEntity>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenDeletingCompletedGoal_DeletesSuccessfully()
    {
        // Arrange
        var goalId = Guid.NewGuid();
        var completedGoal = new GoalEntity
        {
            GoalId = goalId,
            UserId = _testUserId,
            Title = "Completed Goal",
            DurationDays = 30,
            IsCompleted = true,
            CompletedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        };

        var goals = new List<GoalEntity> { completedGoal };
        SetupGoalsDbSet(goals);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new DeleteGoalCommand(goalId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        _mockContext.Verify(x => x.Goals.Remove(It.Is<GoalEntity>(g => g.IsCompleted)), Times.Once);
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
