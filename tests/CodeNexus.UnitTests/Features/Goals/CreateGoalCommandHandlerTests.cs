using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Goals.Commands.CreateGoal;
using CodeNexus.Application.Features.Goals.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using GoalEntity = CodeNexus.Domain.Entities.Goals;
using Moq;
using Xunit;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.UnitTests.Features.Goals;

public class CreateGoalCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<IGoalValidationService> _mockGoalValidationService;
    private readonly CreateGoalCommandHandler _handler;

    public CreateGoalCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockGoalValidationService = new Mock<IGoalValidationService>();
        _handler = new CreateGoalCommandHandler(_mockContext.Object, _mockCurrentUserService.Object, _mockGoalValidationService.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreateGoalSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateGoalCommand("Learn C#", "Master C# programming");
        
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockGoalValidationService.Setup(x => x.IsRelatedToProgrammingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        SetupGoalsDbSet(new List<GoalEntity>());
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
        Assert.False(result.Value.IsSystemDefined);
    }

    [Fact]
    public async Task Handle_WithValidCommandNoDescription_ShouldCreateGoalSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateGoalCommand("Learn Python", null);
        
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockGoalValidationService.Setup(x => x.IsRelatedToProgrammingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        SetupGoalsDbSet(new List<GoalEntity>());
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
    }

    [Fact]
    public async Task Handle_WhenSaveChangesFails_ShouldReturnFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateGoalCommand("Learn Java", "Master Java");
        
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockGoalValidationService.Setup(x => x.IsRelatedToProgrammingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        SetupGoalsDbSet(new List<GoalEntity>());
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
    public async Task Handle_WithInvalidGoal_ShouldReturnFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateGoalCommand("Learn Cooking", "Master cooking skills");
        
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockGoalValidationService.Setup(x => x.IsRelatedToProgrammingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        SetupGoalsDbSet(new List<GoalEntity>());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_GOAL", result.ErrorCode);
        Assert.Contains("programming", result.ErrorMessage);
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
