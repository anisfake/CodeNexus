using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Goals.Commands.CreateGoal;
using CodeNexus.Application.Features.Goals.DTOs;
using CodeNexus.Domain.Entities;
using GoalEntity = CodeNexus.Domain.Entities.Goals;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.Goals;

public class CreateGoalCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly CreateGoalCommandHandler _handler;

    public CreateGoalCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new CreateGoalCommandHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreateGoalSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateGoalCommand("Learn C#", "Master C# programming", 60);
        
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Goals.AddAsync(It.IsAny<GoalEntity>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<GoalEntity>>(
                (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<GoalEntity>)null!));
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Learn C#", result.Value.Title);
        Assert.Equal("Master C# programming", result.Value.Description);
        Assert.Equal(60, result.Value.DurationDays);
    }

    [Fact]
    public async Task Handle_WithValidCommandNoDescription_ShouldCreateGoalSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateGoalCommand("Learn Python", null, 30);
        
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Goals.AddAsync(It.IsAny<GoalEntity>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<GoalEntity>>(
                (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<GoalEntity>)null!));
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Learn Python", result.Value.Title);
        Assert.Null(result.Value.Description);
        Assert.Equal(30, result.Value.DurationDays);
    }

    [Fact]
    public async Task Handle_WhenSaveChangesFails_ShouldReturnFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateGoalCommand("Learn Java", "Master Java", 45);
        
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Goals.AddAsync(It.IsAny<GoalEntity>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<GoalEntity>>(
                (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<GoalEntity>)null!));
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("CREATE_GOAL_FAILED", result.ErrorCode);
        Assert.Contains("Database error", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WithDifferentDurationDays_ShouldCreateGoalWithCorrectDuration()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateGoalCommand("Learn TypeScript", "Master TypeScript", 90);
        
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Goals.AddAsync(It.IsAny<GoalEntity>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<GoalEntity>>(
                (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<GoalEntity>)null!));
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(90, result.Value.DurationDays);
    }
}
