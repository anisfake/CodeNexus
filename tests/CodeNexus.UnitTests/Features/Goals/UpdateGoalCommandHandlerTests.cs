using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Goals.Commands.UpdateGoal;
using CodeNexus.UnitTests.Helpers;
using GoalEntity = CodeNexus.Domain.Entities.Goals;
using Moq;
using Xunit;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.UnitTests.Features.Goals;

public class UpdateGoalCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly UpdateGoalCommandHandler _handler;
    private readonly Guid _testUserId = Guid.NewGuid();

    public UpdateGoalCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(_testUserId);
        _handler = new UpdateGoalCommandHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_WhenGoalExists_UpdatesSuccessfully()
    {
        // Arrange
        var goalId = Guid.NewGuid();
        var existingGoal = new GoalEntity
        {
            GoalId = goalId,
            UserId = _testUserId,
            Title = "Old Title",
            Description = "Old Description",
            DurationDays = 30,
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow
        };

        var goals = new List<GoalEntity> { existingGoal };
        SetupGoalsDbSet(goals);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new UpdateGoalCommand(
            goalId,
            "New Title",
            "New Description",
            DateTime.UtcNow,
            60,
            true
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("New Title", result.Value.Title);
        Assert.Equal("New Description", result.Value.Description);
        Assert.Equal(60, result.Value.DurationDays);
        Assert.True(result.Value.IsCompleted);
        Assert.NotNull(result.Value.CompletedAt);
    }

    [Fact]
    public async Task Handle_WhenGoalNotFound_ReturnsFailure()
    {
        // Arrange
        var goalId = Guid.NewGuid();
        SetupGoalsDbSet(new List<GoalEntity>());

        var command = new UpdateGoalCommand(
            goalId,
            "New Title",
            "New Description",
            null,
            60,
            false
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
        var anotherUserId = Guid.NewGuid();
        var existingGoal = new GoalEntity
        {
            GoalId = goalId,
            UserId = anotherUserId,
            Title = "Old Title",
            DurationDays = 30,
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow
        };

        var goals = new List<GoalEntity> { existingGoal };
        SetupGoalsDbSet(goals);

        var command = new UpdateGoalCommand(goalId, "New Title", null, null, 60, false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("GOAL_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task Handle_WhenMarkingAsIncomplete_ClearsCompletedAt()
    {
        // Arrange
        var goalId = Guid.NewGuid();
        var existingGoal = new GoalEntity
        {
            GoalId = goalId,
            UserId = _testUserId,
            Title = "Completed Goal",
            DurationDays = 30,
            IsCompleted = true,
            CompletedAt = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        };

        var goals = new List<GoalEntity> { existingGoal };
        SetupGoalsDbSet(goals);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new UpdateGoalCommand(goalId, "Updated Goal", null, null, 30, false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsCompleted);
        Assert.Null(result.Value.CompletedAt);
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
