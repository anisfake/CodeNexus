using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.FocusSessions.Commands.StartSession;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using TaskEntity = CodeNexus.Domain.Entities.Tasks;

namespace CodeNexus.UnitTests.Features.FocusSessions;

public class StartSessionCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly StartSessionCommandHandler _handler;

    public StartSessionCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _handler = new StartSessionCommandHandler(_mockContext.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldStartSessionSuccessfully()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var command = new StartSessionCommand(taskId, 25, "Test Session");

        var task = new TaskEntity
        {
            TaskId = taskId,
            Title = "Test Task",
            TaskType = TaskType.Practice,
            Status = TaskStatus_.Pending
        };

        SetupTasksDbSet(new List<TaskEntity> { task });
        SetupFocusSessionsDbSet(new List<FocusSession>());
        
        _mockContext.Setup(x => x.FocusSessions.AddAsync(It.IsAny<FocusSession>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<FocusSession>>(
                (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<FocusSession>)null!));
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(25, result.Value.PlannedDurationMinutes);
        Assert.Contains("started successfully", result.Value.Message);
    }

    [Fact]
    public async Task Handle_WithInvalidTaskId_ShouldReturnFailure()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var command = new StartSessionCommand(taskId, 25, "Test Session");

        SetupTasksDbSet(new List<TaskEntity>());
        SetupFocusSessionsDbSet(new List<FocusSession>());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("TASK_NOT_FOUND", result.ErrorCode);
        Assert.Contains("Task not found", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WithInvalidDuration_ShouldReturnFailure()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var command = new StartSessionCommand(taskId, 3, "Test Session"); // Invalid duration < 5

        var task = new TaskEntity
        {
            TaskId = taskId,
            Title = "Test Task",
            TaskType = TaskType.Practice,
            Status = TaskStatus_.Pending
        };

        SetupTasksDbSet(new List<TaskEntity> { task });
        SetupFocusSessionsDbSet(new List<FocusSession>());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_DURATION", result.ErrorCode);
        Assert.Contains("between 5 and 120 minutes", result.ErrorMessage);
    }

    private void SetupTasksDbSet(List<TaskEntity> tasks)
    {
        var queryable = new TestAsyncEnumerable<TaskEntity>(tasks);
        var dbSetMock = new Mock<DbSet<TaskEntity>>();
        dbSetMock.As<IQueryable<TaskEntity>>().Setup(m => m.Provider).Returns(queryable.AsQueryable().Provider);
        dbSetMock.As<IQueryable<TaskEntity>>().Setup(m => m.Expression).Returns(queryable.AsQueryable().Expression);
        dbSetMock.As<IQueryable<TaskEntity>>().Setup(m => m.ElementType).Returns(queryable.AsQueryable().ElementType);
        dbSetMock.As<IQueryable<TaskEntity>>().Setup(m => m.GetEnumerator()).Returns(queryable.AsQueryable().GetEnumerator());
        dbSetMock.As<IAsyncEnumerable<TaskEntity>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(queryable.GetAsyncEnumerator());
        _mockContext.Setup(x => x.Tasks).Returns(dbSetMock.Object);
    }

    private void SetupFocusSessionsDbSet(List<FocusSession> sessions)
    {
        var queryable = new TestAsyncEnumerable<FocusSession>(sessions);
        var dbSetMock = new Mock<DbSet<FocusSession>>();
        dbSetMock.As<IQueryable<FocusSession>>().Setup(m => m.Provider).Returns(queryable.AsQueryable().Provider);
        dbSetMock.As<IQueryable<FocusSession>>().Setup(m => m.Expression).Returns(queryable.AsQueryable().Expression);
        dbSetMock.As<IQueryable<FocusSession>>().Setup(m => m.ElementType).Returns(queryable.AsQueryable().ElementType);
        dbSetMock.As<IQueryable<FocusSession>>().Setup(m => m.GetEnumerator()).Returns(queryable.AsQueryable().GetEnumerator());
        dbSetMock.As<IAsyncEnumerable<FocusSession>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(queryable.GetAsyncEnumerator());
        _mockContext.Setup(x => x.FocusSessions).Returns(dbSetMock.Object);
    }
}