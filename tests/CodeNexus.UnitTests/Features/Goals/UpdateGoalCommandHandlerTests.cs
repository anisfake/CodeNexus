using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Goals.Commands.UpdateGoal;
using CodeNexus.Domain.Enums;
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
        var existingGoal = new GoalEntity
        {
            GoalId = goalId,
            CreatedByUserId = _testUserId,
            Title = "Old Title",
            Description = "Old Description",
            IsSystemDefined = false,
            IsActive = true,
            Duration = GoalDuration.OneMonth,
            CreatedAt = DateTime.UtcNow
        };

        var goals = new List<GoalEntity> { existingGoal };
        SetupGoalsDbSet(goals);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _mockGoalValidationService.Setup(x => x.IsRelatedToProgrammingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new UpdateGoalCommand(
            goalId,
            "New Title",
            "New Description",
            true,
            GoalDuration.TwoMonths
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("New Title", result.Value.Title);
        Assert.Equal("New Description", result.Value.Description);
        Assert.True(result.Value.IsActive);
        Assert.Equal(GoalDuration.TwoMonths, result.Value.Duration);
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
            true,
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
        var anotherUserId = Guid.NewGuid();
        var existingGoal = new GoalEntity
        {
            GoalId = goalId,
            CreatedByUserId = anotherUserId,
            Title = "Old Title",
            IsSystemDefined = false,
            IsActive = true,
            Duration = GoalDuration.OneMonth,
            CreatedAt = DateTime.UtcNow
        };

        var goals = new List<GoalEntity> { existingGoal };
        SetupGoalsDbSet(goals);

        var command = new UpdateGoalCommand(goalId, "New Title", null, true, GoalDuration.TwoMonths);

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
        var existingGoal = new GoalEntity
        {
            GoalId = goalId,
            CreatedByUserId = null,
            Title = "System Goal",
            IsSystemDefined = true,
            IsActive = true,
            Duration = GoalDuration.OneMonth,
            CreatedAt = DateTime.UtcNow
        };

        var goals = new List<GoalEntity> { existingGoal };
        SetupGoalsDbSet(goals);

        var command = new UpdateGoalCommand(goalId, "Updated Title", null, true, GoalDuration.ThreeMonths);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("GOAL_NOT_FOUND", result.ErrorCode);
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