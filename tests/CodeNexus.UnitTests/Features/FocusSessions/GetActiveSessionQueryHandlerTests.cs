using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.FocusSessions.Queries.GetActiveSession;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using TaskEntity = CodeNexus.Domain.Entities.Tasks;

namespace CodeNexus.UnitTests.Features.FocusSessions;

public class GetActiveSessionQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly GetActiveSessionQueryHandler _handler;

    public GetActiveSessionQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _handler = new GetActiveSessionQueryHandler(_mockContext.Object);
    }

    [Fact]
    public async Task Handle_WithActiveRunningSession_ShouldReturnSessionDetails()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var query = new GetActiveSessionQuery(taskId);
        var startTime = DateTime.UtcNow.AddMinutes(-15);

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
            Title = "Focus Session",
            SessionStatus = SessionStatus.Running,
            StartTime = startTime,
            PlannedDurationMinutes = 25
        };

        SetupFocusSessionsDbSet(new List<FocusSession> { session });

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(sessionId, result.Value.SessionId);
        Assert.Equal(taskId, result.Value.TaskId);
        Assert.Equal("Focus Session", result.Value.Title);
        Assert.Equal(25, result.Value.PlannedDurationMinutes);
        Assert.Equal(15, result.Value.ElapsedMinutes);
        Assert.Equal(10, result.Value.RemainingMinutes);
        Assert.Equal(SessionStatus.Running.ToString(), result.Value.SessionStatus);
        Assert.False(result.Value.IsOvertime);
    }



    [Fact]
    public async Task Handle_WithOvertimeSession_ShouldReturnOvertimeStatus()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var query = new GetActiveSessionQuery(taskId);
        var startTime = DateTime.UtcNow.AddMinutes(-30); // 30 minutes ago

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
            Title = "Overtime Session",
            SessionStatus = SessionStatus.Running,
            StartTime = startTime,
            PlannedDurationMinutes = 25 // Planned for 25 minutes, but running for 30
        };

        SetupFocusSessionsDbSet(new List<FocusSession> { session });

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(30, result.Value.ElapsedMinutes);
        Assert.Equal(-5, result.Value.RemainingMinutes); // Negative means overtime
        Assert.True(result.Value.IsOvertime);
    }

    [Fact]
    public async Task Handle_WithNoActiveSession_ShouldReturnFailure()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var query = new GetActiveSessionQuery(taskId);

        // Setup with no sessions
        SetupFocusSessionsDbSet(new List<FocusSession>());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("NO_ACTIVE_SESSION", result.ErrorCode);
        Assert.Contains("No active session found", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WithOnlyCompletedSessions_ShouldReturnFailure()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var query = new GetActiveSessionQuery(taskId);

        var task = new TaskEntity
        {
            TaskId = taskId,
            Title = "Test Task",
            TaskType = TaskType.Practice
        };

        var completedSession = new FocusSession
        {
            SessionId = sessionId,
            TaskId = taskId,
            Task = task,
            Title = "Completed Session",
            SessionStatus = SessionStatus.CompletedOnTime, // Not active
            StartTime = DateTime.UtcNow.AddMinutes(-30),
            EndTime = DateTime.UtcNow.AddMinutes(-5),
            PlannedDurationMinutes = 25,
            ActualDurationMinutes = 25
        };

        SetupFocusSessionsDbSet(new List<FocusSession> { completedSession });

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("NO_ACTIVE_SESSION", result.ErrorCode);
        Assert.Contains("No active session found", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WithMultipleActiveSessions_ShouldReturnMostRecent()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var query = new GetActiveSessionQuery(taskId);

        var task = new TaskEntity
        {
            TaskId = taskId,
            Title = "Test Task",
            TaskType = TaskType.Practice
        };

        var olderSession = new FocusSession
        {
            SessionId = Guid.NewGuid(),
            TaskId = taskId,
            Task = task,
            Title = "Older Session",
            SessionStatus = SessionStatus.Running,
            StartTime = DateTime.UtcNow.AddMinutes(-30),
            PlannedDurationMinutes = 25
        };

        var newerSession = new FocusSession
        {
            SessionId = Guid.NewGuid(),
            TaskId = taskId,
            Task = task,
            Title = "Newer Session",
            SessionStatus = SessionStatus.Running,
            StartTime = DateTime.UtcNow.AddMinutes(-10),
            PlannedDurationMinutes = 25
        };

        SetupFocusSessionsDbSet(new List<FocusSession> { olderSession, newerSession });

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Newer Session", result.Value.Title);
        Assert.Equal(10, result.Value.ElapsedMinutes);
    }

    [Fact]
    public async Task Handle_ShouldCalculateTimesCorrectly()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var query = new GetActiveSessionQuery(taskId);
        var startTime = DateTime.UtcNow.AddMinutes(-12); // Exactly 12 minutes ago

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
            Title = "Time Test Session",
            SessionStatus = SessionStatus.Running,
            StartTime = startTime,
            PlannedDurationMinutes = 20
        };

        SetupFocusSessionsDbSet(new List<FocusSession> { session });

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(result.Value.ElapsedMinutes >= 11 && result.Value.ElapsedMinutes <= 13); // Allow for timing differences
        Assert.True(result.Value.RemainingMinutes >= 7 && result.Value.RemainingMinutes <= 9);
        Assert.False(result.Value.IsOvertime);
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
