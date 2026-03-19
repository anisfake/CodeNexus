using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.FocusSessions.Commands.AbandonSession;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using TaskEntity = CodeNexus.Domain.Entities.Tasks;

namespace CodeNexus.UnitTests.Features.FocusSessions;

public class AbandonSessionCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly AbandonSessionCommandHandler _handler;

    public AbandonSessionCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _handler = new AbandonSessionCommandHandler(_mockContext.Object);
    }

    [Fact]
    public async Task Handle_WithValidRunningSession_ShouldAbandonSuccessfully()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var command = new AbandonSessionCommand(sessionId);

        var task = new TaskEntity
        {
            TaskId = taskId,
            Title = "Test Task",
            TaskType = TaskType.Practice
        };

        var session = new FocusSession
        {
            SessionId = sessionId,
            TaskId = taskId,
            Task = task,
            SessionStatus = SessionStatus.Running,
            StartTime = DateTime.UtcNow.AddMinutes(-15),
            PlannedDurationMinutes = 25
        };

        SetupFocusSessionsDbSet(new List<FocusSession> { session });
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(SessionStatus.Abandoned, session.SessionStatus);
        Assert.NotNull(session.EndTime);
        Assert.NotNull(session.ActualDurationMinutes);
        Assert.True(session.ActualDurationMinutes > 0);
    }

    [Fact]
    public async Task Handle_WithSessionNotFound_ShouldReturnFailure()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var command = new AbandonSessionCommand(sessionId);

        SetupFocusSessionsDbSet(new List<FocusSession>());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("SESSION_NOT_FOUND", result.ErrorCode);
        Assert.Contains("Session not found", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WithAlreadyCompletedSession_ShouldReturnFailure()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var command = new AbandonSessionCommand(sessionId);

        var task = new TaskEntity
        {
            TaskId = taskId,
            Title = "Test Task",
            TaskType = TaskType.Practice
        };

        var session = new FocusSession
        {
            SessionId = sessionId,
            TaskId = taskId,
            Task = task,
            SessionStatus = SessionStatus.CompletedOnTime, // Already completed
            StartTime = DateTime.UtcNow.AddMinutes(-30),
            EndTime = DateTime.UtcNow.AddMinutes(-5),
            PlannedDurationMinutes = 25,
            ActualDurationMinutes = 25
        };

        SetupFocusSessionsDbSet(new List<FocusSession> { session });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("SESSION_NOT_ACTIVE", result.ErrorCode);
        Assert.Contains("not active", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WithAlreadyAbandonedSession_ShouldReturnFailure()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var command = new AbandonSessionCommand(sessionId);

        var task = new TaskEntity
        {
            TaskId = taskId,
            Title = "Test Task",
            TaskType = TaskType.Practice
        };

        var session = new FocusSession
        {
            SessionId = sessionId,
            TaskId = taskId,
            Task = task,
            SessionStatus = SessionStatus.Abandoned, // Already abandoned
            StartTime = DateTime.UtcNow.AddMinutes(-30),
            EndTime = DateTime.UtcNow.AddMinutes(-10),
            PlannedDurationMinutes = 25,
            ActualDurationMinutes = 20
        };

        SetupFocusSessionsDbSet(new List<FocusSession> { session });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("SESSION_NOT_ACTIVE", result.ErrorCode);
        Assert.Contains("not active", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WhenSaveChangesFails_ShouldReturnFailure()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var command = new AbandonSessionCommand(sessionId);

        var task = new TaskEntity
        {
            TaskId = taskId,
            Title = "Test Task",
            TaskType = TaskType.Practice
        };

        var session = new FocusSession
        {
            SessionId = sessionId,
            TaskId = taskId,
            Task = task,
            SessionStatus = SessionStatus.Running,
            StartTime = DateTime.UtcNow.AddMinutes(-15),
            PlannedDurationMinutes = 25
        };

        SetupFocusSessionsDbSet(new List<FocusSession> { session });
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("ABANDON_SESSION_FAILED", result.ErrorCode);
        Assert.Contains("Database error", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_ShouldCalculateCorrectActualDuration()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var command = new AbandonSessionCommand(sessionId);
        var startTime = DateTime.UtcNow.AddMinutes(-17); // 17 minutes ago

        var task = new TaskEntity
        {
            TaskId = taskId,
            Title = "Test Task",
            TaskType = TaskType.Practice
        };

        var session = new FocusSession
        {
            SessionId = sessionId,
            TaskId = taskId,
            Task = task,
            SessionStatus = SessionStatus.Running,
            StartTime = startTime,
            PlannedDurationMinutes = 25
        };

        SetupFocusSessionsDbSet(new List<FocusSession> { session });
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(session.ActualDurationMinutes);
        Assert.True(session.ActualDurationMinutes >= 16 && session.ActualDurationMinutes <= 18); // Allow for small timing differences
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
